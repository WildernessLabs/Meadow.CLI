using Meadow.Hardware;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Diagnostics;
using System.IO.Ports;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Meadow.Hcom;

public partial class SerialConnection : ConnectionBase, IDisposable
{
    public const int DefaultBaudRate = 115200;
    public const int ReadBufferSizeBytes = 0x2000;
    private const int DefaultTimeout = 5000;

    private event EventHandler<string>? FileReadCompleted = delegate { };
    private event EventHandler? FileWriteAccepted;
    private event EventHandler<string>? FileDataReceived;

    private SerialPort _port = default!;
    private ILogger? _logger;
    private bool _isDisposed;
    private List<IConnectionListener> _listeners = new List<IConnectionListener>();
    private Queue<IRequest> _pendingCommands = new Queue<IRequest>();
    private bool _maintainConnection;
    private Thread? _connectionManager = null;
    private List<string> _textList = new List<string>();
    private int _messageCount = 0;
    private ReadFileInfo? _readFileInfo = null;
    private string? _lastError = null;

    public override string Name { get; }

    public SerialConnection(string port, ILogger? logger = default)
    {
        if (!SerialPort.GetPortNames().Contains(port, StringComparer.InvariantCultureIgnoreCase))
        {
            throw new ArgumentException($"Serial Port '{port}' not found.");
        }

        Name = port;
        State = ConnectionState.Disconnected;
        _logger = logger;

        CreatePort();

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

    private void CreatePort()
    {
        _port = new SerialPort(Name);
        _port.ReadTimeout = _port.WriteTimeout = DefaultTimeout;
        _port.Open();
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
            Open(true);
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

    private void Open(bool inLoop = false)
    {
        if (!_port.IsOpen)
        {
            try
            {
                Debug.WriteLine("Opening COM port...");
                _port.Open();
            }
            catch (UnauthorizedAccessException ex)
            {
                // Handle unauthorized access (e.g., port in use by another application)
                throw new Exception($"Serial port '{_port.PortName}' is in use by another application.", ex.InnerException);
            }
            catch (IOException ex)
            {
                // Handle I/O errors
                throw new Exception($"An I/O error occurred when opening the serial port '{_port.PortName}'.", ex.InnerException);
            }
            catch (TimeoutException ex)
            {
                // Handle timeout
                throw new Exception($"Timeout occurred when opening the serial port '{_port.PortName}'.", ex.InnerException);
            }
        }
        else if (inLoop)
        {
            Thread.Sleep(1000);
        }

        State = ConnectionState.Connected;

        Debug.WriteLine("Opened COM port");
    }

    private void Close()
    {
        if (_port.IsOpen)
        {
            try
            {
                _port.Close();
            }
            catch (IOException ex)
            {
                // Handle I/O errors
                throw new Exception($"An I/O error occurred when attempting to close the serial port '{_port.PortName}'.", ex.InnerException);
            }
        }

        State = ConnectionState.Disconnected;
    }

    public override async Task<IMeadowDevice?> Attach(CancellationToken? cancellationToken = null, int timeoutSeconds = 10)
    {
        try
        {
            // ensure the port is open
            Open();

            // search for the device via HCOM - we'll use a simple command since we don't have a "ping"
            var command = RequestBuilder.Build<GetDeviceInfoRequest>();

            // sequence numbers are only for file retrieval.  Setting it to non-zero will cause it to hang

            _port.DiscardInBuffer();

            // wait for a response
            var timeout = timeoutSeconds * 2;
            var dataReceived = false;

            // local function so we can unsubscribe
            var count = _messageCount;

            _pendingCommands.Enqueue(command);

            while (timeout-- > 0)
            {
                if (cancellationToken?.IsCancellationRequested ?? false) return null;
                if (timeout <= 0) throw new TimeoutException();

                if (count != _messageCount)
                {
                    dataReceived = true;
                    break;
                }

                await Task.Delay(500);
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

    private async void CommandManager()
    {
        await Task.Run(() =>
        {
            while (!_isDisposed)
            {
                while (_pendingCommands.Count > 0)
                {
                    Debug.WriteLine($"There are {_pendingCommands.Count} pending commands");

                    var command = _pendingCommands.Dequeue() as Request;

                    // if this is a file write, we need to packetize for progress

                    if (command != null)
                    {
                        var payload = command.Serialize();
                        EncodeAndSendPacket(payload);
                    }

                    // TODO: re-queue on fail?
                }

                Thread.Sleep(1000);
            }
        });
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

                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Path.GetFileName(MeadowFileName));
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

        _pendingCommands.Enqueue(command);
    }

    private void EncodeAndSendPacket(byte[] messageBytes, CancellationToken? cancellationToken = null)
    {
        EncodeAndSendPacket(messageBytes, messageBytes.Length, cancellationToken);
    }

    private void EncodeAndSendPacket(byte[] messageBytes, int length, CancellationToken? cancellationToken = null)
    {
        Debug.WriteLine($"+EncodeAndSendPacket({length} bytes)");

        while (!_port.IsOpen)
        {
            State = ConnectionState.Disconnected;
            Thread.Sleep(100);
            // wait for the port to open
        }

        State = ConnectionState.Connected;

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
                //                Debug.Write($"Sending {encodedToSend} bytes...");
                //await _port.BaseStream.WriteAsync(encodedBytes, 0, encodedToSend, cancellationToken ?? CancellationToken.None);
                _port.Write(encodedBytes, 0, encodedToSend);
                //                Debug.WriteLine($"sent");
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
            _segments = new List<Memory<byte>>();
            _segments.Add(segment);
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
            //_logger?.LogTrace("Throwing out 0x00 from buffer");
            return false;
        }

        var decodedSize = CobsTools.CobsDecoding(packetBuffer.ToArray(), packetLength, ref decodedBuffer);

        /*
        // If a message is too short it is ignored
        if (decodedSize < MeadowDeviceManager.ProtocolHeaderSize)
        {
            return false;
        }

        Debug.Assert(decodedSize <= MeadowDeviceManager.MaxAllowableMsgPacketLength);

        // Process the received packet
        ParseAndProcessReceivedPacket(decodedBuffer.AsSpan(0, decodedSize).ToArray(),
                                      cancellationToken);

        */
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

    private const string RuntimeSucessfullyEnabledToken = "Meadow successfully started MONO";
    private const string RuntimeStateToken = "Mono is";
    private const string RuntimeIsEnabledToken = "Mono is enabled";
    private const string RuntimeIsDisabledToken = "Mono is disabled";
    private const string RuntimeHasBeenToken = "Mono has been";
    private const string RuntimeHasBeenEnabledToken = "Mono has been enabled";
    private const string RuntimeHasBeenDisabledToken = "Mono has been disabled";
    private const string RtcRetrievalToken = "UTC time:";

    public int CommandTimeoutSeconds { get; set; } = 30;

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

        return await WaitForInformationResponse(RuntimeStateToken, RuntimeIsEnabledToken, cancellationToken);
    }

    private async Task<bool> WaitForInformationResponse(string[] textToWaitOn, CancellationToken? cancellationToken)
    {
        // wait for an information response
        var timeout = CommandTimeoutSeconds * 2;
        while (timeout-- > 0)
        {
            if (cancellationToken?.IsCancellationRequested ?? false)
                return false;
            if (timeout <= 0)
                throw new TimeoutException();

            foreach (var t in textToWaitOn)
            {
                if (InfoMessages.Any(m => m.Contains(t))) return true;
            }

            await Task.Delay(500);
        }
        return false;
    }

    private async Task<bool> WaitForInformationResponse(string textToContain, string textToVerify, CancellationToken? cancellationToken)
    {
        // wait for an information response
        var timeout = CommandTimeoutSeconds * 2;
        while (timeout-- > 0)
        {
            if (cancellationToken?.IsCancellationRequested ?? false)
                return false;
            if (timeout <= 0)
                throw new TimeoutException();

            if (InfoMessages.Count > 0)
            {
                var m = InfoMessages.FirstOrDefault(i => i.Contains(textToContain));
                if (m != null)
                {
                    return m == textToVerify;
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

        // if the runtime and OS mismatch, we get "Mono disabled" otehrwise we get "Mono is disabled". Yay!
        await WaitForInformationResponse(new string[] { "Mono disabled", RuntimeHasBeenEnabledToken }, cancellationToken);
    }

    public override async Task RuntimeDisable(CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<RuntimeDisableRequest>();

        InfoMessages.Clear();

        _lastRequestConcluded = null;

        EnqueueRequest(command);

        // if the runtime and OS mismatch, we get "Mono disabled" otehrwise we get "Mono is disabled". Yay!
        await WaitForInformationResponse(new string[] { "Mono disabled", RuntimeIsDisabledToken }, cancellationToken);
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
        await Task.Delay(500);

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

    public override async Task<MeadowFileInfo[]?> GetFileList(bool includeCrcs, CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<GetFileListRequest>();
        command.IncludeCrcs = includeCrcs;

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
            var fi = MeadowFileInfo.Parse(candidate);
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


            /*
            RaiseConnectionMessage("\nErasing runtime flash blocks...");
            status = await WaitForResult(() =>
            {
                if (_lastRequestConcluded != null)
                {
                    // happens on error
                    return true;
                }

                var m = string.Join('\n', InfoMessages);
                return m.Contains("Mono memory erase success");
            },
            cancellationToken);

            InfoMessages.Clear();

            RaiseConnectionMessage("Moving runtime to flash...");

            status = await WaitForResult(() =>
            {
                if (_lastRequestConcluded != null)
                {
                    // happens on error
                    return true;
                }

                var m = string.Join('\n', InfoMessages);
                return m.Contains("Verifying runtime flash operation.");
            },
            cancellationToken);

            InfoMessages.Clear();

            RaiseConnectionMessage("Verifying...");

            status = await WaitForResult(() =>
            {
                if (_lastRequestConcluded != null)
                {
                    return true;
                }

                return false;
            },
            cancellationToken);
            */

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

        Debug.WriteLine($"Sending '{localFileName}'");

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

            if (!await WaitForResult(
                () =>
                {
                    return completed | ex != null;
                },
                cancellationToken))
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

        FileDataReceived += OnFileDataReceived;

        _lastRequestConcluded = null;
        EnqueueRequest(command);

        await WaitForConcluded(null, cancellationToken);

        return contents;
    }

    public override async Task DeleteFile(string meadowFileName, CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<FileDeleteRequest>();
        command.MeadowFileName = meadowFileName;

        _lastRequestConcluded = null;

        EnqueueRequest(command);

        await WaitForConcluded(null, cancellationToken);
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

        string? contents = null;

        void OnFileDataReceived(object? sender, string data)
        {
            contents = data;
        }

        FileDataReceived += OnFileDataReceived;

        var lastTimeout = CommandTimeoutSeconds;

        CommandTimeoutSeconds = 5 * 60;

        _lastRequestConcluded = null;
        EnqueueRequest(command);

        await WaitForConcluded(null, cancellationToken);

        CommandTimeoutSeconds = lastTimeout;

        return contents!;
    }

    public override async Task<DebuggingServer> StartDebuggingSession(int port, ILogger? logger, CancellationToken cancellationToken)
    {
        if (Device != null)
        {
            logger?.LogDebug($"Start Debugging on port: {port}");
            await Device.StartDebugging(port, logger, cancellationToken);

            /* TODO logger?.LogDebug("Reinitialize the device");
            await ReInitializeMeadow(cancellationToken); */

            var endpoint = new IPEndPoint(IPAddress.Loopback, port);
            var debuggingServer = new DebuggingServer(Device, endpoint, logger!);

            logger?.LogDebug("Tell the Debugging Server to Start Listening");
            await debuggingServer.StartListening(cancellationToken);
            return debuggingServer;
        }
        else
        {
            throw new DeviceNotFoundException();
        }
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
}