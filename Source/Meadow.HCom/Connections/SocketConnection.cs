using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Meadow.Hcom;

public partial class SocketConnection : ConnectionBase, IDisposable
{
    public const int ReadBufferSizeBytes = 0x2000;
    private const int DefaultTimeout = 5000;

    private event EventHandler FileWriteAccepted = default!;
    private event EventHandler<string> FileTextReceived = default!;

    private readonly string _host;
    private readonly int _port;
    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private readonly ILogger? _logger;
    private bool _isDisposed;
    private ConnectionState _state;
    private readonly List<IConnectionListener> _listeners = new List<IConnectionListener>();
    private readonly ConcurrentQueue<IRequest> _commandQueue = new ConcurrentQueue<IRequest>();
    private readonly AutoResetEvent _commandEvent = new AutoResetEvent(false);
    private readonly object _connectLock = new object();
    private readonly List<string> _textList = new List<string>();
    private int _messageCount = 0;
    private ReadFileInfo? _readFileInfo = null;
    private string? _lastError = null;

    public override string Name { get; }

    public SocketConnection(string host, int port, ILogger? logger = default)
    {
        _host = host;
        _port = port;
        _logger = logger;

        Name = $"socket://{host}:{port}";
        State = ConnectionState.Disconnected;

        new Task(
            () => _ = ListenerProc(),
            TaskCreationOptions.LongRunning)
        .Start();

        new Thread(CommandManager)
        {
            IsBackground = true,
            Name = "HCOM Socket Sender"
        }
        .Start();
    }

    public override ConnectionState State
    {
        get => _state;
        protected set
        {
            if (value == State) { return; }
            _state = value;
        }
    }

    private bool IsConnected => _tcpClient?.Connected ?? false;

    private void Open()
    {
        lock (_connectLock)
        {
            if (!IsConnected)
            {
                try
                {
                    var client = new TcpClient();
                    client.Connect(_host, _port);
                    client.Client.NoDelay = true; // disable Nagle — send HCOM packets immediately
                    client.ReceiveTimeout = DefaultTimeout;
                    client.SendTimeout = DefaultTimeout;
                    _networkStream = client.GetStream();
                    _tcpClient = client;
                    State = ConnectionState.Connected;
                }
                catch (SocketException se)
                {
                    throw new Exception($"Unable to connect to socket '{_host}:{_port}' - {se.Message}");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Unable to connect to socket '{_host}:{_port}' - {ex.Message}");
                }
            }
        }
    }

    private void Close()
    {
        try
        {
            _networkStream?.Dispose();
            _tcpClient?.Dispose();
        }
        catch { }

        _networkStream = null;
        _tcpClient = null;
        State = ConnectionState.Disconnected;
    }

    public override void Detach()
    {
        Close();
    }

    public override async Task<IMeadowDevice?> Attach(CancellationToken? cancellationToken = null, int timeoutSeconds = 10)
    {
        try
        {
            Open();

            var command = RequestBuilder.Build<GetDeviceInfoRequest>();

            var timeout = timeoutSeconds * 50;
            var dataReceived = false;

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
        while (!IsConnected)
        {
            _state = ConnectionState.Disconnected;
            Thread.Sleep(100);
        }

        _state = ConnectionState.Connected;

        try
        {
            int encodedToSend;
            byte[] encodedBytes;

            try
            {
                var l = Protocol.HCOM_PROTOCOL_ENCODED_MAX_SIZE + (Protocol.HCOM_PROTOCOL_ENCODED_MAX_SIZE / 254) + 8;
                encodedBytes = new byte[l + 2];

                // Skip over first byte so it can be a start delimiter
                encodedToSend = CobsTools.CobsEncoding(messageBytes, 0, length, ref encodedBytes, 1);

                if (encodedToSend == -1)
                {
                    _logger?.LogError($"Error - encodedToSend == -1");
                    return;
                }
            }
            catch (Exception except)
            {
                string msg = string.Format("Send setup Exception: {0}", except);
                _logger?.LogError(msg);
                throw;
            }

            // Add delimiters to packet boundaries
            encodedBytes[0] = 0;                // Start delimiter
            encodedToSend++;
            encodedBytes[encodedToSend] = 0;    // End delimiter
            encodedToSend++;

            try
            {
                _networkStream!.Write(encodedBytes, 0, encodedToSend);
            }
            catch (IOException ioe)
            {
                _logger?.LogError($"Write failed: {ioe.Message}");
                throw;
            }
            catch (ObjectDisposedException ode)
            {
                _logger?.LogError($"Write failed - stream disposed: {ode.Message}");
                throw;
            }
        }
        catch (Exception except)
        {
            _logger?.LogError($"EncodeAndSendPacket threw: {except}");
            throw;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                Close();
            }

            _isDisposed = true;
        }
    }

    // ----------------------------------------------
    // Command methods
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

            if (_lastError != null)
            {
                return true;
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

    public override async Task SetDeveloperParameter(ushort parameter, uint value, TimeSpan timeout, CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<DeveloperRequest>();
        command.ExtraData = parameter;
        command.UserData = value;

        CommandTimeoutSeconds = (int)timeout.TotalSeconds;

        _lastRequestConcluded = null;

        EnqueueRequest(command);

        await WaitForConcluded(null, cancellationToken);
    }

    public override async Task ResetDevice(CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<ResetDeviceRequest>();

        EnqueueRequest(command);

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
            () =>
            {
                if (!string.IsNullOrWhiteSpace(_lastError))
                {
                    throw new Exception(_lastError);
                }

                return _textListComplete ?? false;
            }, cancellationToken))
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
        const int maxRetries = 10;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            if (cancellationToken?.IsCancellationRequested ?? false) return false;

            try
            {
                var result = await WriteFile(localFileName, meadowFileName,
                    RequestType.HCOM_MDOW_REQUEST_START_FILE_TRANSFER,
                    RequestType.HCOM_MDOW_REQUEST_END_FILE_TRANSFER,
                    0,
                    cancellationToken);

                if (result)
                {
                    return true;
                }
            }
            catch (TimeoutException)
            {
                // WaitForResult timed out waiting for firmware acknowledgment
            }

            Debug.WriteLine($"WriteFile attempt {attempt}/{maxRetries} failed for '{Path.GetFileName(localFileName)}', retrying...");
            await Task.Delay(200);
        }

        return false;
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
        var commandTimeout = CommandTimeoutSeconds;
        _lastRequestConcluded = null;

        CommandTimeoutSeconds = 180;
        InfoMessages.Clear();

        try
        {
            RaiseConnectionMessage($"Transferring {Path.GetFileName(localFileName)} to coprocessor...");

            if (!await WriteFile(localFileName, null,
                RequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER,
                RequestType.HCOM_MDOW_REQUEST_END_ESP_FILE_TRANSFER,
                destinationAddress,
                cancellationToken))
            {
                return false;
            }

            _lastRequestConcluded = null;

            await WaitForConcluded(null, cancellationToken);
            return true;
        }
        finally
        {
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
            Debug.WriteLine($"WriteFile: FileWriteAccepted event fired");
            accepted = true;
        }
        void OnFileError(object? sender, Exception exception)
        {
            Debug.WriteLine($"WriteFile: FileException event fired: {exception.Message}");
            ex = exception;
        }
        var dataTransferStarted = false;

        void OnFileRetry(object? sender, EventArgs e)
        {
            if (!dataTransferStarted)
            {
                Debug.WriteLine($"WriteFile: FileWriteFailed before data transfer — ignoring stale watchdog");
                return;
            }
            Debug.WriteLine($"WriteFile: FileWriteFailed event fired (needsRetry)");
            needsRetry = true;
        }

        FileWriteAccepted += OnFileWriteAccepted;
        FileException += OnFileError;
        FileWriteFailed += OnFileRetry;

        // Increase timeout for file operations — emulated targets need more time
        // for flash writes (virtual-time UART + LittleFS operations are slower).
        var savedTimeout = CommandTimeoutSeconds;
        CommandTimeoutSeconds = Math.Max(CommandTimeoutSeconds, 120);

        Debug.WriteLine($"WriteFile: EnqueueRequest for '{meadowFileName ?? Path.GetFileName(localFileName)}'");
        EnqueueRequest(command);

        if (!await WaitForResult(
                () =>
                {
                    if (ex != null) throw ex;
                    return accepted;
                },
                cancellationToken))
        {
            Debug.WriteLine($"WriteFile: WaitForResult returned false (accepted={accepted}, needsRetry={needsRetry})");
            return false;
        }

        Debug.WriteLine($"WriteFile: WaitForResult returned true (accepted={accepted}, needsRetry={needsRetry})");

        byte[] packet = new byte[Protocol.HCOM_PROTOCOL_PACKET_MAX_SIZE - 2];
        ushort sequenceNumber = 0;

        var progress = 0;
        var expected = fileBytes.Length;

        var fileName = Path.GetFileName(localFileName);

        base.RaiseFileWriteProgress(fileName, progress, expected);

        dataTransferStarted = true;
        Debug.WriteLine($"WriteFile: entering data loop, needsRetry={needsRetry}, fileSize={expected}");
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

            // Yield briefly to allow the receiving end to process data.
            // Critical for emulated targets where the firmware's virtual-time
            // UART processing lags behind wall-clock TCP sends.
            await Task.Delay(1);
        }

        Debug.WriteLine($"WriteFile: data loop done, progress={progress}/{expected}, needsRetry={needsRetry}");

        if (!needsRetry)
        {
            base.RaiseFileWriteProgress(fileName, expected, expected);

            var request = RequestBuilder.Build<EndFileWriteRequest>();
            request.SetRequestType(endRequestType);
            var p = request.Serialize();

            EncodeAndSendPacket(p, cancellationToken);

            // After END_FILE_TRANSFER, the firmware closes the file, reads it back
            // for CRC verification, then sends TEXT_INFORMATION and clears download
            // state. We must wait for this to complete before starting the next file.
            // On real hardware this is near-instant, but under emulation the firmware's
            // virtual-time processing can lag significantly behind wall time.
            // Wait for a TEXT_INFORMATION response (download result) or timeout.
            var infoCountBefore = InfoMessages.Count;
            var endWaitStart = Environment.TickCount;
            while (InfoMessages.Count <= infoCountBefore)
            {
                if (cancellationToken?.IsCancellationRequested ?? false) break;
                if (Environment.TickCount - endWaitStart > CommandTimeoutSeconds * 1000) break;
                await Task.Delay(100);
            }
            Debug.WriteLine($"WriteFile: EndFileWrite wait done ({Environment.TickCount - endWaitStart}ms, newMsgs={InfoMessages.Count - infoCountBefore})");
        }
        else
        {
            Debug.WriteLine($"WriteFile: needsRetry=true, returning false");
        }

        FileWriteAccepted -= OnFileWriteAccepted;
        FileException -= OnFileError;
        FileWriteFailed -= OnFileRetry;

        CommandTimeoutSeconds = savedTimeout;
        return !needsRetry;
    }

    public override async Task<bool> ReadFile(string meadowFileName, string? localFileName = null, CancellationToken? cancellationToken = null)
    {
        var command = RequestBuilder.Build<InitFileReadRequest>();
        command.MeadowFileName = meadowFileName;
        command.LocalFileName = localFileName;

        Exception? ex = null;

        void OnFileError(object? sender, Exception exception)
        {
            ex = exception;
        }

        try
        {
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

    public override async Task<DebuggingServer> StartDebuggingSession(int port, ILogger? logger, CancellationToken cancellationToken, string debuggerName = "Visual Studio")
    {
        if (Device == null)
        {
            throw new DeviceNotFoundException();
        }

        var debuggingServer = new DebuggingServer(this, port, logger, debuggerName);

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
