using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Security.Cryptography;

namespace Meadow.Hcom;

public delegate void ConnectionStateChangedHandler(SerialConnection connection, ConnectionState oldState, ConnectionState newState);

public partial class SerialConnection : ConnectionBase, IDisposable
{
    public const int DefaultBaudRate = 115200;
    public const int ReadBufferSizeBytes = 0x2000;
    private const int DefaultTimeout = 5000;

    private event EventHandler FileWriteAccepted = default!;
    private event EventHandler<string> FileTextReceived = default!;
    public event ConnectionStateChangedHandler ConnectionStateChanged = default!;

    private readonly SerialPort _port;
    private readonly ILogger? _logger;
    private bool _isDisposed;
    private ConnectionState _state;
    private readonly List<IConnectionListener> _listeners = new List<IConnectionListener>();
    private readonly ConcurrentQueue<IRequest> _commandQueue = new ConcurrentQueue<IRequest>();
    private readonly AutoResetEvent _commandEvent = new AutoResetEvent(false);
    private bool _maintainConnection;
    private Thread? _connectionManager = null;
    private readonly List<string> _textList = new List<string>();
    private int _messageCount = 0;
    private ReadFileInfo? _readFileInfo = null;
    private string? _lastError = null;

    public override string Name { get; }

    public bool AggressiveReconnectEnabled { get; set; } = false;

    public SerialConnection(string port, ILogger? logger = default)
    {
        if (!SerialPort.GetPortNames().Contains(port, StringComparer.InvariantCultureIgnoreCase))
        {
            throw new ArgumentException($"Serial Port '{port}' not found.");
        }

        Name = port;
        State = ConnectionState.Disconnected;
        _logger = logger;
        _port = new SerialPort(port);
        _port.ReadTimeout = _port.WriteTimeout = DefaultTimeout;

        new Task(
            () => _ = ListenerProc(),
            TaskCreationOptions.LongRunning)
        .Start();

        new Thread(CommandManager)
        {
            IsBackground = true,
            Name = "HCOM Sender"
        }
        .Start();
    }

    private bool MaintainConnection
    {
        get => _maintainConnection;
        set
        {
            if (value == MaintainConnection) return;

            _maintainConnection = value;

            if (value)
            {
                if (_connectionManager == null || _connectionManager.ThreadState != System.Threading.ThreadState.Running)
                {
                    _connectionManager = new Thread(ConnectionManagerProc)
                    {
                        IsBackground = true,
                        Name = "HCOM Connection Manager"
                    };
                    _connectionManager.Start();
                }
            }
        }
    }

    private void ConnectionManagerProc()
    {
        while (_maintainConnection)
        {
            if (!_port.IsOpen)
            {
                try
                {
                    Debug.WriteLine("Opening COM port...");
                    Open();
                    Debug.WriteLine("Opened COM port");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"{ex.Message}");
                    Thread.Sleep(1000);
                }
            }
            else
            {
                Thread.Sleep(1000);
            }
        }
    }

    public void AddListener(IConnectionListener listener)
    {
        lock (_listeners)
        {
            _listeners.Add(listener);
        }

        Open();

        MaintainConnection = true;
    }

    public void RemoveListener(IConnectionListener listener)
    {
        lock (_listeners)
        {
            _listeners.Remove(listener);
        }

        // TODO: stop maintaining connection?
    }

    public override ConnectionState State
    {
        get => _state;
        protected set
        {
            if (value == State) { return; }

            var old = _state;
            _state = value;
            ConnectionStateChanged?.Invoke(this, old, State);
        }
    }

    private void Open()
    {
        if (!_port.IsOpen)
        {
            try
            {
                _port.Open();
            }
            catch (FileNotFoundException)
            {
                throw new Exception($"Serial port '{_port.PortName}' not found");
            }
            catch (UnauthorizedAccessException uae)
            {
                throw new Exception($"{uae.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to open port '{_port.PortName}' - {ex.Message}");
            }

            State = ConnectionState.Connected;
        }
    }

    private void Close()
    {
        if (_port.IsOpen)
        {
            _port.Close();
        }

        State = ConnectionState.Disconnected;
    }

    public override void Detach()
    {
        if (MaintainConnection)
        {
            // TODO: close this up
        }

        Close();
    }

    public override async Task<IMeadowDevice?> Attach(CancellationToken? cancellationToken = null, int timeoutSeconds = 10)
    {
        try
        {
            // ensure the port is open
            Open();

            // search for the device via HCOM - we'll use a simple command since we don't have a "ping"
            var command = RequestBuilder.Build<GetDeviceInfoRequest>();

            // sequence numbers are only for file retrieval - Setting it to non-zero will cause it to hang

            _port.DiscardInBuffer();

            // wait for a response
            var timeout = timeoutSeconds * 50;
            var dataReceived = false;

            // local function so we can unsubscribe
            var count = _messageCount;

            EnqueueRequest(command);

            while (timeout-- > 0)
            {
                if (cancellationToken?.IsCancellationRequested ?? false) return null;
                if (timeout <= 0) throw new TimeoutException();

                if (count != _messageCount)
                {
                    dataReceived = true;
                    break;
                }

                await Task.Delay(20);
            }

            // if HCOM fails, check for DFU/bootloader mode?  only if we're doing an OS thing, so maybe no

            // create the device instance
            if (dataReceived)
            {
                Device = new MeadowDevice(this);
            }

            return Device;
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Failed to connect");
            throw;
        }
    }

    private void CommandManager()
    {
        while (!_isDisposed)
        {
            _commandEvent.WaitOne(1000);

            while (_commandQueue.Count > 0)
            {
                Debug.WriteLine($"There are {_commandQueue.Count} pending commands");

                _commandQueue.TryDequeue(out var pendingCommand);

                if (pendingCommand is Request command)
                {
                    // if this is a file write, we need to packetize for progress
                    var payload = command.Serialize();
                    EncodeAndSendPacket(payload);
                }
            }
        }
    }

    private class ReadFileInfo
    {
        private string? _localFileName;

        public string MeadowFileName { get; set; } = default!;
        public string? LocalFileName
        {
            get
            {
                if (_localFileName != null) return _localFileName;

                return Path.Combine(Environment.CurrentDirectory, Path.GetFileName(MeadowFileName));
            }
            set => _localFileName = value;
        }
        public FileStream FileStream { get; set; } = default!;
    }

    public void EnqueueRequest(IRequest command)
    {
        // TODO: verify we're connected

        if (command is InitFileReadRequest sfr)
        {
            _readFileInfo = new ReadFileInfo
            {
                MeadowFileName = sfr.MeadowFileName,
                LocalFileName = sfr.LocalFileName,
            };
        }

        _commandQueue.Enqueue(command);
        _commandEvent.Set();
    }

    private void EncodeAndSendPacket(byte[] messageBytes, CancellationToken? cancellationToken = null)
    {
        EncodeAndSendPacket(messageBytes, messageBytes.Length, cancellationToken);
    }

    private void EncodeAndSendPacket(byte[] messageBytes, int length, CancellationToken? cancellationToken = null)
    {
        while (!_port.IsOpen)
        {
            _state = ConnectionState.Disconnected;
            Thread.Sleep(100);
            // wait for the port to open
        }

        _state = ConnectionState.Connected;

        try
        {
            int encodedToSend;
            byte[] encodedBytes;

            // For file download this is a LOT of messages
            // _uiSupport.WriteDebugLine($"Sending packet with {messageSize} bytes");

            // For testing calculate the crc including the sequence number
            //_packetCrc32 = NuttxCrc.Crc32part(messageBytes, messageSize, 0, _packetCrc32);
            try
            {
                // The encoded size using COBS is just a bit more than the original size adding 1 byte
                // every 254 bytes plus 1 and need room for beginning and ending delimiters.
                var l = Protocol.HCOM_PROTOCOL_ENCODED_MAX_SIZE + (Protocol.HCOM_PROTOCOL_ENCODED_MAX_SIZE / 254) + 8;
                encodedBytes = new byte[l + 2];

                // Skip over first byte so it can be a start delimiter
                encodedToSend = CobsTools.CobsEncoding(messageBytes, 0, length, ref encodedBytes, 1);

                // DEBUG TESTING
                if (encodedToSend == -1)
                {
                    _logger?.LogError($"Error - encodedToSend == -1");
                    return;
                }

                if (_port == null)
                {
                    _logger?.LogError($"Error - SerialPort == null");
                    throw new Exception("Port is null");
                }
            }
            catch (Exception except)
            {
                string msg = string.Format("Send setup Exception: {0}", except);
                _logger?.LogError(msg);
                throw;
            }

            // Add delimiters to packet boundaries
            try
            {
                encodedBytes[0] = 0;                // Start delimiter
                encodedToSend++;
                encodedBytes[encodedToSend] = 0;    // End delimiter
                encodedToSend++;
            }
            catch (Exception encodedBytesEx)
            {
                // This should drop the connection and retry
                Debug.WriteLine($"Adding encodeBytes delimiter threw: {encodedBytesEx}");
                Thread.Sleep(500);    // Place for break point
                throw;
            }

            try
            {
                // Send the data to Meadow
                // DO NOT USE _port.BaseStream.  It disables port timeouts!
                _port.Write(encodedBytes, 0, encodedToSend);
            }
            catch (InvalidOperationException ioe)  // Port not opened
            {
                string msg = string.Format("Write but port not opened. Exception: {0}", ioe);
                _logger?.LogError(msg);
                throw;
            }
            catch (ArgumentOutOfRangeException aore)  // offset or count don't match buffer
            {
                string msg = string.Format("Write buffer, offset and count don't line up. Exception: {0}", aore);
                _logger?.LogError(msg);
                throw;
            }
            catch (ArgumentException ae)  // offset plus count > buffer length
            {
                string msg = string.Format($"Write offset plus count > buffer length. Exception: {0}", ae);
                _logger?.LogError(msg);
                throw;
            }
            catch (TimeoutException te) // Took too long to send
            {
                string msg = string.Format("Write took too long to send. Exception: {0}", te);
                _logger?.LogError(msg);
                throw;
            }
        }
        catch (Exception except)
        {
            // DID YOU RESTART MEADOW?
            // This should drop the connection and retry
            _logger?.LogError($"EncodeAndSendPacket threw: {except}");
            throw;
        }
    }

    private class SerialMessage
    {
        private readonly IList<Memory<byte>> _segments;

        public SerialMessage(Memory<byte> segment)
        {
            _segments = new List<Memory<byte>>
            {
                segment
            };
        }

        public void AddSegment(Memory<byte> segment)
        {
            _segments.Add(segment);
        }

        public byte[] ToArray()
        {
            using var ms = new MemoryStream();
            foreach (var segment in _segments)
            {
                // We could just call ToArray on the `Memory` but that will result in an uncontrolled allocation.
                var tmp = ArrayPool<byte>.Shared.Rent(segment.Length);
                segment.CopyTo(tmp);
                ms.Write(tmp, 0, segment.Length);
                ArrayPool<byte>.Shared.Return(tmp);
            }
            return ms.ToArray();
        }
    }

    private bool DecodeAndProcessPacket(Memory<byte> packetBuffer, CancellationToken cancellationToken)
    {
        var decodedBuffer = ArrayPool<byte>.Shared.Rent(8192);
        var packetLength = packetBuffer.Length;
        // It's possible that we may find a series of 0x00 values in the buffer.
        // This is because when the sender is blocked (because this code isn't
        // running) it will attempt to send a single 0x00 before the full message.
        // This allows it to test for a connection. When the connection is
        // unblocked this 0x00 is sent and gets put into the buffer along with
        // any others that were queued along the usb serial pipe line.
        if (packetLength == 1)
        {
            //_logger.LogTrace("Throwing out 0x00 from buffer");
            return false;
        }

        var decodedSize = CobsTools.CobsDecoding(packetBuffer.ToArray(), packetLength, ref decodedBuffer);

        ArrayPool<byte>.Shared.Return(decodedBuffer);
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                Close();
                _port.Dispose();
            }

            _isDisposed = true;
        }
    }

    // ----------------------------------------------
    // ----------------------------------------------
    // ----------------------------------------------

    private Exception? _lastException;
    private bool? _textListComplete;
    private DeviceInfo? _deviceInfo;
    private RequestType? _lastRequestConcluded = null;
    private List<string> StdOut { get; } = new List<string>();
    private List<string> StdErr { get; } = new List<string>();
    private List<string> InfoMessages { get; } = new List<string>();

    private const string MonoStateToken = "Mono is";
    private const string RuntimeStateToken = "Runtime is";
    private const string MonoIsEnabledToken = "Mono is enabled";
    private const string RuntimeIsEnabledToken = "Runtime is enabled";
    private const string RtcRetrievalToken = "UTC time:";

    public int CommandTimeoutSeconds { get; set; } = 30;

    private async Task<bool> WaitForFileReadCompleted(CancellationToken? cancellationToken)
    {
        var timeout = CommandTimeoutSeconds * 2;

        var completed = false;

        void LocalFRCHandler(object s, string e)
        {
            completed = true;
        }
        void LocalFBRHandler(object s, int e)
        {
            timeout = CommandTimeoutSeconds * 2;
        }

        FileBytesReceived += LocalFBRHandler;
        FileReadCompleted += LocalFRCHandler;

        try
        {
            while (timeout-- > 0)
            {
                if (cancellationToken?.IsCancellationRequested ?? false) return false;
                if (_lastException != null) return false;

                if (timeout <= 0) throw new TimeoutException();

                if (completed) return true;

                await Task.Delay(500);
            }
        }
        finally
        {
            // clean up local events
            FileBytesReceived -= LocalFBRHandler;
            FileReadCompleted -= LocalFRCHandler;
        }

        return true;
    }

    private async Task<bool> WaitForResult(Func<bool> checkAction, CancellationToken? cancellationToken)
    {
        var timeout = CommandTimeoutSeconds * 2;

        while (timeout-- > 0)
        {
            if (cancellationToken?.IsCancellationRequested ?? false) return false;
            if (_lastException != null) return false;

            if (timeout <= 0) throw new TimeoutException();

            if (checkAction())
            {
                break;
            }

            await Task.Delay(500);
        }

        return true;
    }

    private async Task<bool> WaitForResponseText(string textToAwait, CancellationToken? cancellationToken = null)
    {
        return await WaitForResult(() =>
        {
            if (InfoMessages.Count > 0)
            {
                var m = InfoMessages.FirstOrDefault(i => i.Contains(textToAwait));
                if (m != null)
                {
                    return true;
                }
            }

            return false;
        }, cancellationToken);
    }

    private async Task<bool> WaitForConcluded(RequestType? requestType = null, CancellationToken? cancellationToken = null)
    {
        return await WaitForResult(() =>
        {
            if (_lastRequestConcluded != null)
            {
                if (requestType == null || requestType == _lastRequestConcluded)
                {
                    return true;
                }
            }

            return false;
        }, cancellationToken);
    }

    public override async Task SetRtcTime(DateTimeOffset dateTime, CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<SetRtcTimeRequest>();
        command.Time = dateTime;

        _lastRequestConcluded = null;

        EnqueueRequest(command);

        var success = await WaitForResult(() =>
        {
            if (_lastRequestConcluded != null && _lastRequestConcluded == RequestType.HCOM_MDOW_REQUEST_RTC_SET_TIME_CMD)
            {
                return true;
            }

            return false;
        }, cancellationToken);
    }

    public override async Task<DateTimeOffset?> GetRtcTime(CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<GetRtcTimeRequest>();

        InfoMessages.Clear();

        EnqueueRequest(command);

        DateTimeOffset? now = null;

        var success = await WaitForResult(() =>
        {
            if (InfoMessages.Count > 0)
            {
                var m = InfoMessages.FirstOrDefault(i => i.Contains(RtcRetrievalToken));
                if (m != null)
                {
                    var timeString = m.Substring(m.IndexOf(RtcRetrievalToken) + RtcRetrievalToken.Length);
                    now = DateTimeOffset.Parse(timeString);
                    return true;
                }
            }

            return false;
        }, cancellationToken);

        return now;
    }

    public override async Task<bool> IsRuntimeEnabled(CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<GetRuntimeStateRequest>();

        InfoMessages.Clear();

        EnqueueRequest(command);

        // wait for an information response
        var timeout = CommandTimeoutSeconds * 2;
        while (timeout-- > 0)
        {
            if (cancellationToken?.IsCancellationRequested ?? false)
            {
                return false;
            }
            if (timeout <= 0)
            {
                throw new TimeoutException();
            }

            if (InfoMessages.Count > 0)
            {
                var m = InfoMessages.FirstOrDefault(i =>
                    i.Contains(RuntimeStateToken) ||
                    i.Contains(MonoStateToken));
                if (m != null)
                {
                    return (m == RuntimeIsEnabledToken) || (m == MonoIsEnabledToken);
                }
            }

            await Task.Delay(500);
        }
        return false;
    }

    public override async Task RuntimeEnable(CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<RuntimeEnableRequest>();

        InfoMessages.Clear();

        _lastRequestConcluded = null;

        EnqueueRequest(command);

        await WaitForConcluded(null, cancellationToken);
    }

    public override async Task RuntimeDisable(CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<RuntimeDisableRequest>();

        InfoMessages.Clear();

        _lastRequestConcluded = null;

        EnqueueRequest(command);

        await WaitForConcluded(null, cancellationToken);
    }

    public override async Task NshEnable(CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<NshEnableDisableRequest>(1);

        InfoMessages.Clear();

        _lastRequestConcluded = null;

        EnqueueRequest(command);

        await WaitForConcluded(null, cancellationToken);
    }

    public override async Task NshDisable(CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<NshEnableDisableRequest>(0);

        InfoMessages.Clear();

        _lastRequestConcluded = null;

        EnqueueRequest(command);

        await WaitForConcluded(null, cancellationToken);
    }

    public override async Task TraceEnable(CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<TraceEnableRequest>();

        _lastRequestConcluded = null;

        EnqueueRequest(command);

        await WaitForConcluded(null, cancellationToken);
    }

    public override async Task TraceDisable(CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<TraceDisableRequest>();

        _lastRequestConcluded = null;

        EnqueueRequest(command);

        await WaitForConcluded(null, cancellationToken);
    }

    public override async Task UartTraceEnable(CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<UartTraceEnableRequest>();

        _lastRequestConcluded = null;

        EnqueueRequest(command);

        await WaitForConcluded(null, cancellationToken);
    }

    public override async Task UartProfilerEnable(CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<UartProfilerEnableRequest>();

        _lastRequestConcluded = null;

        EnqueueRequest(command);

        await WaitForConcluded(null, cancellationToken);
    }

    public override async Task UartProfilerDisable(CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<UartProfilerDisableRequest>();

        _lastRequestConcluded = null;

        EnqueueRequest(command);

        await WaitForConcluded(null, cancellationToken);
    }

    public override async Task UartTraceDisable(CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<UartTraceDisableRequest>();

        _lastRequestConcluded = null;

        EnqueueRequest(command);

        await WaitForConcluded(null, cancellationToken);
    }

    public override async Task SetTraceLevel(int level, CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<TraceLevelRequest>();
        command.UserData = (uint)level;

        _lastRequestConcluded = null;

        EnqueueRequest(command);

        await WaitForConcluded(null, cancellationToken);
    }

    public override async Task SetDeveloperParameter(ushort parameter, uint value, CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<DeveloperRequest>();
        command.ExtraData = parameter;
        command.UserData = value;

        _lastRequestConcluded = null;

        EnqueueRequest(command);

        await WaitForConcluded(null, cancellationToken);
    }

    public override async Task ResetDevice(CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<ResetDeviceRequest>();

        EnqueueRequest(command);

        // we have to give time for the device to actually reset
        await Task.Delay(1500);

        await WaitForMeadowAttach(cancellationToken);
    }

    public override async Task<DeviceInfo?> GetDeviceInfo(CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<GetDeviceInfoRequest>();

        _deviceInfo = null;

        _lastException = null;
        EnqueueRequest(command);

        if (!await WaitForResult(
            () => _deviceInfo != null,
            cancellationToken))
        {
            return null;
        }

        return _deviceInfo;
    }

    public override async Task<MeadowFileInfo[]?> GetFileList(string folder, bool includeCrcs, CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<GetFileListRequest>();
        command.IncludeCrcs = includeCrcs;

        command.Path = folder;

        EnqueueRequest(command);

        if (!await WaitForResult(
            () => _textListComplete ?? false,
            cancellationToken))
        {
            _textListComplete = null;
            return null;
        }

        var list = new List<MeadowFileInfo>();

        foreach (var candidate in _textList)
        {
            var fi = MeadowFileInfo.Parse(candidate, folder);
            if (fi != null)
            {
                list.Add(fi);
            }
        }

        _textListComplete = null;
        return list.ToArray();
    }

    public override async Task<bool> WriteFile(
        string localFileName,
        string? meadowFileName = null,
        CancellationToken? cancellationToken = null)
    {
        return await WriteFile(localFileName, meadowFileName,
            RequestType.HCOM_MDOW_REQUEST_START_FILE_TRANSFER,
            RequestType.HCOM_MDOW_REQUEST_END_FILE_TRANSFER,
            0,
            cancellationToken);
    }

    public override async Task<bool> WriteRuntime(
        string localFileName,
        CancellationToken? cancellationToken = null)
    {
        var commandTimeout = CommandTimeoutSeconds;


        CommandTimeoutSeconds = 120;
        _lastRequestConcluded = null;

        try
        {
            InfoMessages.Clear();

            _lastRequestConcluded = null;

            var status = await WriteFile(localFileName, "Meadow.OS.Runtime.bin",
                RequestType.HCOM_MDOW_REQUEST_MONO_UPDATE_RUNTIME,
                RequestType.HCOM_MDOW_REQUEST_MONO_UPDATE_FILE_END,
                0,
                cancellationToken);


            if (status)
            {
                await WaitForConcluded(null, cancellationToken);
            }

            return status;
        }
        finally
        {
            CommandTimeoutSeconds = commandTimeout;
        }
    }

    public override async Task<bool> WriteCoprocessorFile(
        string localFileName,
        int destinationAddress,
        CancellationToken? cancellationToken = null)
    {
        // make the timeouts much bigger, as the ESP flash takes a lot of time
        var readTimeout = _port.ReadTimeout;
        var commandTimeout = CommandTimeoutSeconds;
        _lastRequestConcluded = null;

        _port.ReadTimeout = 60000;
        CommandTimeoutSeconds = 180;
        InfoMessages.Clear();

        try
        {
            RaiseConnectionMessage($"Transferring {Path.GetFileName(localFileName)} to coprocessor...");

            // push the file to the device
            if (!await WriteFile(localFileName, null,
                RequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER,
                RequestType.HCOM_MDOW_REQUEST_END_ESP_FILE_TRANSFER,
                destinationAddress,
                cancellationToken))
            {
                return false;
            }


            _lastRequestConcluded = null;

            // now wait for the STM32 to finish writing to the ESP32
            await WaitForConcluded(null, cancellationToken);
            return true;
        }
        finally
        {
            _port.ReadTimeout = readTimeout;
            CommandTimeoutSeconds = commandTimeout;
        }
    }

    private async Task<bool> WriteFile(
        string localFileName,
        string? meadowFileName,
        RequestType initialRequestType,
        RequestType endRequestType,
        int writeAddress = 0,
        CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<InitFileWriteRequest>();

        var fileBytes = File.ReadAllBytes(localFileName);

        var fileHash = Encoding.ASCII.GetBytes("12345678901234567890123456789012"); // must be 32 bytes
        if (writeAddress != 0)
        {
            // calculate the MD5 hash of the file - we have to send it as a UTF8 string, not as bytes.
            using var md5 = MD5.Create();
            var hashBytes = md5.ComputeHash(fileBytes);
            var hashString = BitConverter.ToString(hashBytes)
                .Replace("-", "")
                .ToLowerInvariant();
            fileHash = Encoding.UTF8.GetBytes(hashString);
        }
        var fileCrc = NuttxCrc.Crc32part(fileBytes, (uint)fileBytes.Length, 0);

        command.SetParameters(
            localFileName,
            meadowFileName ?? Path.GetFileName(localFileName),
            fileCrc,
            writeAddress,
            fileHash,
            initialRequestType);

        var accepted = false;
        Exception? ex = null;
        var needsRetry = false;

        void OnFileWriteAccepted(object? sender, EventArgs a)
        {
            accepted = true;
        }
        void OnFileError(object? sender, Exception exception)
        {
            ex = exception;
        }
        void OnFileRetry(object? sender, EventArgs e)
        {
            needsRetry = true;
        }

        FileWriteAccepted += OnFileWriteAccepted;
        FileException += OnFileError;
        FileWriteFailed += OnFileRetry;

        EnqueueRequest(command);

        // this will wait for a "file write accepted" from the target
        if (!await WaitForResult(
                () =>
                {
                    if (ex != null) throw ex;
                    return accepted;
                },
                cancellationToken))
        {
            return false;
        }

        // now send the file data
        // The maximum data bytes is max packet size - 2 bytes for the sequence number
        byte[] packet = new byte[Protocol.HCOM_PROTOCOL_PACKET_MAX_SIZE - 2];
        ushort sequenceNumber = 0;

        var progress = 0;
        var expected = fileBytes.Length;

        var fileName = Path.GetFileName(localFileName);

        base.RaiseFileWriteProgress(fileName, progress, expected);

        var oldTimeout = _port.ReadTimeout;
        _port.ReadTimeout = 60000;

        while (true && !needsRetry)
        {
            if (cancellationToken.HasValue && cancellationToken.Value.IsCancellationRequested)
            {
                return false;
            }

            sequenceNumber++;

            Array.Copy(BitConverter.GetBytes(sequenceNumber), packet, 2);

            var toRead = fileBytes.Length - progress;
            if (toRead > packet.Length - 2)
            {
                toRead = packet.Length - 2;
            }
            Array.Copy(fileBytes, progress, packet, 2, toRead);
            try
            {
                EncodeAndSendPacket(packet, toRead + 2, cancellationToken);
            }
            catch (Exception)
            {
                break;
            }

            progress += toRead;
            base.RaiseFileWriteProgress(fileName, progress, expected);
            if (progress >= fileBytes.Length) break;
        }

        if (!needsRetry)
        {
            _port.ReadTimeout = oldTimeout;

            base.RaiseFileWriteProgress(fileName, expected, expected);

            // finish with an "end" message - not enqued because this is all a serial operation
            var request = RequestBuilder.Build<EndFileWriteRequest>();
            request.SetRequestType(endRequestType);
            var p = request.Serialize();
            EncodeAndSendPacket(p, cancellationToken);
        }

        FileWriteAccepted -= OnFileWriteAccepted;
        FileException -= OnFileError;
        FileWriteFailed -= OnFileRetry;

        return !needsRetry;
    }

    public override async Task<bool> ReadFile(string meadowFileName, string? localFileName = null, CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<InitFileReadRequest>();
        command.MeadowFileName = meadowFileName;
        command.LocalFileName = localFileName;

        var completed = false;
        Exception? ex = null;

        void OnFileReadCompleted(object? sender, string filename)
        {
            completed = true;
        }
        void OnFileError(object? sender, Exception exception)
        {
            ex = exception;
        }

        try
        {
            FileReadCompleted += OnFileReadCompleted;
            FileException += OnFileError;
            ConnectionError += OnFileError;

            EnqueueRequest(command);

            if (!await WaitForFileReadCompleted(cancellationToken))
            {
                return false;
            }

            return ex == null;
        }
        finally
        {
            FileReadCompleted -= OnFileReadCompleted;
            FileException -= OnFileError;
        }
    }

    public override async Task<string?> ReadFileString(string fileName, CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<FileInitialBytesRequest>();
        command.MeadowFileName = fileName;

        string? contents = null;

        void OnFileDataReceived(object? sender, string data)
        {
            contents = data;
        }

        FileTextReceived += OnFileDataReceived;

        _lastRequestConcluded = null;
        EnqueueRequest(command);

        await WaitForConcluded(null, cancellationToken);

        return contents;
    }

    public override async Task<bool> DeleteFile(string meadowFileName, CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<FileDeleteRequest>();
        command.MeadowFileName = meadowFileName;

        _lastRequestConcluded = null;

        EnqueueRequest(command);

        var result = await WaitForConcluded(null, cancellationToken);
        return result;
    }

    public override async Task EraseFlash(CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<FlashEraseRequest>();

        _lastRequestConcluded = null;

        var lastTimeout = CommandTimeoutSeconds;

        CommandTimeoutSeconds = 5 * 60;

        EnqueueRequest(command);

        await WaitForConcluded(null, cancellationToken);

        CommandTimeoutSeconds = lastTimeout;
    }

    public override async Task<string> GetPublicKey(CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<GetPublicKeyRequest>();

        string contents = string.Empty;

        void OnFileDataReceived(object? sender, string data)
        {
            contents = data;
        }

        FileTextReceived += OnFileDataReceived;

        var lastTimeout = CommandTimeoutSeconds;

        CommandTimeoutSeconds = 5 * 60;

        _lastRequestConcluded = null;
        EnqueueRequest(command);

        if (!await WaitForResult(
                        () =>
                        {
                            return contents != string.Empty;
                        },
                        cancellationToken))
        {
            CommandTimeoutSeconds = lastTimeout;
            return string.Empty;
        }

        CommandTimeoutSeconds = lastTimeout;

        return contents;
    }

    public override async Task<DebuggingServer> StartDebuggingSession(int port, ILogger? logger, CancellationToken cancellationToken)
    {
        if (Device == null)
        {
            throw new DeviceNotFoundException();
        }

        AggressiveReconnectEnabled = true;

        var debuggingServer = new DebuggingServer(this, port, logger);

        Debug.WriteLine("You can now connect the debugger client to the local tunnel port");
        await debuggingServer.StartListening(cancellationToken);

        Debug.WriteLine($"Debugger client is connected!!! Port: {port}");
        await Device.StartDebugging(port, logger, cancellationToken);
        Debug.WriteLine("Debugging has fully started!!");

        return debuggingServer;
    }

    public override async Task StartDebugging(int port, ILogger? logger, CancellationToken? cancellationToken)
    {
        var command = RequestBuilder.Build<StartDebuggingRequest>();

        if (command != null)
        {
            InfoMessages.Clear();

            _lastRequestConcluded = null;

            EnqueueRequest(command);

            await WaitForMeadowAttach(cancellationToken);
        }
        else
        {
            new Exception($"{typeof(StartDebuggingRequest)} command failed to build");
        }
    }

    public override Task SendDebuggerData(byte[] debuggerData, uint userData, CancellationToken? cancellationToken)
    {
        var command = RequestBuilder.Build<DebuggerDataRequest>(userData);
        command.DebuggerData = debuggerData;

        _lastRequestConcluded = null;

        EnqueueRequest(command);

        return Task.CompletedTask;
    }
}