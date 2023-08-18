using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Diagnostics;
using System.IO.Ports;

namespace Meadow.Hcom;

public delegate void ConnectionStateChangedHandler(SerialConnection connection, ConnectionState oldState, ConnectionState newState);

public partial class SerialConnection : ConnectionBase, IDisposable
{
    public const int DefaultBaudRate = 115200;
    public const int ReadBufferSizeBytes = 0x2000;

    private event EventHandler<string> FileReadCompleted = delegate { };
    private event EventHandler FileWriteAccepted;

    public event ConnectionStateChangedHandler ConnectionStateChanged = delegate { };

    private SerialPort _port;
    private ILogger? _logger;
    private bool _isDisposed;
    private ConnectionState _state;
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
        _port = new SerialPort(port);
        _port.ReadTimeout = _port.WriteTimeout = 5000;

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
                    _port.Open();
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

    public ConnectionState State
    {
        get => _state;
        private set
        {
            if (value == State) return;

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
        }
        State = ConnectionState.Connected;
    }

    private void Close()
    {
        if (_port.IsOpen)
        {
            _port.Close();
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

    private void CommandManager()
    {
        while (!_isDisposed)
        {
            while (_pendingCommands.Count > 0)
            {
                Debug.WriteLine($"There are {_pendingCommands.Count} pending commands");

                var command = _pendingCommands.Dequeue() as Request;

                // if this is a file write, we need to packetize for progress

                var payload = command.Serialize();
                EncodeAndSendPacket(payload);

                // TODO: re-queue on fail?
            }

            Thread.Sleep(1000);
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

    private void EncodeAndSendPacket(byte[] messageBytes)
    {
        EncodeAndSendPacket(messageBytes, messageBytes.Length);
    }

    private void EncodeAndSendPacket(byte[] messageBytes, int length)
    {
        Debug.WriteLine($"+EncodeAndSendPacket({length} bytes)");

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
                encodedBytes = new byte[Protocol.HCOM_PROTOCOL_ENCODED_MAX_SIZE];

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
                Debug.WriteLine($"Sending {encodedToSend} bytes...");
                _port.Write(encodedBytes, 0, encodedToSend);
                Debug.WriteLine($"sent");
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
            //_logger.LogTrace("Throwing out 0x00 from buffer");
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

    protected virtual void Dispose(bool disposing)
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

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    // ----------------------------------------------
    // ----------------------------------------------
    // ----------------------------------------------

    private Exception? _lastException;
    private bool? _textListComplete;

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

    private DeviceInfo? _deviceInfo;
    private int? _lastRequestConcluded = null;
    private List<string> StdOut { get; } = new List<string>();
    private List<string> StdErr { get; } = new List<string>();
    private List<string> InfoMessages { get; } = new List<string>();

    private const string RuntimeSucessfullyEnabledToken = "Meadow successfully started MONO";
    private const string RuntimeSucessfullyDisabledToken = "Mono is disabled";
    private const string RuntimeStateToken = "Mono is";
    private const string RuntimeIsEnabledToken = "Mono is enabled";
    private const string RtcRetrievalToken = "UTC time:";

    public override async Task SetRtcTime(DateTimeOffset dateTime, CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<SetRtcTimeRequest>();
        command.Time = dateTime;

        _lastRequestConcluded = null;

        EnqueueRequest(command);

        var success = await WaitForResult(() =>
        {
            if (_lastRequestConcluded != null && _lastRequestConcluded == 0x303)
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
            if (cancellationToken?.IsCancellationRequested ?? false) return false;
            if (timeout <= 0) throw new TimeoutException();

            if (InfoMessages.Count > 0)
            {
                var m = InfoMessages.FirstOrDefault(i => i.Contains(RuntimeStateToken));
                if (m != null)
                {
                    return m == RuntimeIsEnabledToken;
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

        EnqueueRequest(command);

        // we have to give time for the device to actually reset
        await Task.Delay(500);

        var success = await WaitForResult(() =>
        {
            if (InfoMessages.Count > 0)
            {
                var m = InfoMessages.FirstOrDefault(i => i.Contains(RuntimeSucessfullyEnabledToken));
                if (m != null)
                {
                    return true;
                }
            }

            return false;
        }, cancellationToken);

        if (!success) throw new Exception("Unable to enable runtime");
    }

    public override async Task RuntimeDisable(CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<RuntimeDisableRequest>();

        InfoMessages.Clear();

        EnqueueRequest(command);

        // we have to give time for the device to actually reset
        await Task.Delay(500);

        var success = await WaitForResult(() =>
        {
            if (InfoMessages.Count > 0)
            {
                var m = InfoMessages.FirstOrDefault(i => i.Contains(RuntimeSucessfullyDisabledToken));
                if (m != null)
                {
                    return true;
                }
            }

            return false;
        }, cancellationToken);

        if (!success) throw new Exception("Unable to disable runtime");
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

    public override async Task<bool> WriteFile(string localFileName, string? meadowFileName = null, CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<InitFileWriteRequest>();
        command.SetParameters(localFileName, meadowFileName ?? Path.GetFileName(localFileName));

        var accepted = false;
        Exception? ex = null;

        void OnFileWriteAccepted(object? sender, EventArgs a)
        {
            accepted = true;
        }
        void OnFileError(object? sender, Exception exception)
        {
            ex = exception;
        }

        FileWriteAccepted += OnFileWriteAccepted;
        FileException += OnFileError;

        EnqueueRequest(command);

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

        using FileStream fs = File.OpenRead(localFileName);

        // The maximum data bytes is max packet size - 2 bytes for the sequence number
        byte[] packet = new byte[Protocol.HCOM_PROTOCOL_PACKET_MAX_SIZE - 2];
        int bytesRead;
        ushort sequenceNumber = 0;

        while (true)
        {
            if (cancellationToken.HasValue && cancellationToken.Value.IsCancellationRequested)
            {
                return false;
            }

            sequenceNumber++;

            // sequenc number at the start of the packet
            Array.Copy(BitConverter.GetBytes(sequenceNumber), packet, 2);
            // followed by the file data
            bytesRead = fs.Read(packet, 2, packet.Length - 2);
            if (bytesRead <= 0) break;
            EncodeAndSendPacket(packet, bytesRead + 2);

        }

        // finish with an "end" message - not enqued because this is all a serial operation
        var request = RequestBuilder.Build<EndFileWriteRequest>();
        var p = request.Serialize();
        EncodeAndSendPacket(p);

        return true;

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

            EnqueueRequest(command);

            if (!await WaitForResult(
                () =>
                {
                    if (ex != null) throw ex;
                    return completed;
                },
                cancellationToken))
            {
                return false;
            }

            return true;
        }
        finally
        {
            FileReadCompleted -= OnFileReadCompleted;
            FileException -= OnFileError;
        }
    }
}