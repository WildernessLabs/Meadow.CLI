using System.Diagnostics;

namespace Meadow.Hcom;

public abstract class ConnectionBase : IMeadowConnection, IDisposable
{
    private readonly Queue<IRequest> _pendingCommands = new();
    private ReadFileInfo? _readFileInfo = null;
    private bool _isDisposed;

    protected List<IConnectionListener> ConnectionListeners { get; } = new();

    public ConnectionState State { get; protected set; }
    public IMeadowDevice? Device { get; protected set; }

    public event EventHandler<Exception> ConnectionError;

    internal abstract Task DeliverRequest(IRequest request);
    public abstract string Name { get; }

    public abstract Task<IMeadowDevice?> Attach(CancellationToken? cancellationToken = null, int timeoutSeconds = 10);
    public abstract Task<DeviceInfo?> GetDeviceInfo(CancellationToken? cancellationToken = null);
    public abstract Task<MeadowFileInfo[]?> GetFileList(bool includeCrcs, CancellationToken? cancellationToken = null);
    public abstract Task<bool> WriteFile(string localFileName, string? meadowFileName = null, CancellationToken? cancellationToken = null);
    public abstract Task<bool> ReadFile(string meadowFileName, string? localFileName = null, CancellationToken? cancellationToken = null);
    public abstract Task Reset(CancellationToken? cancellationToken = null);
    public abstract Task<bool> IsRuntimeEnabled(CancellationToken? cancellationToken = null);
    public abstract Task RuntimeDisable(CancellationToken? cancellationToken = null);
    public abstract Task RuntimeEnable(CancellationToken? cancellationToken = null);
    public abstract Task<DateTimeOffset?> GetRtcTime(CancellationToken? cancellationToken = null);
    public abstract Task SetRtcTime(DateTimeOffset dateTime, CancellationToken? cancellationToken = null);

    public ConnectionBase()
    {
        new Thread(CommandManager)
        {
            IsBackground = true,
            Name = "HCOM Sender"
        }
        .Start();
    }

    protected void RaiseConnectionError(Exception error)
    {
        ConnectionError?.Invoke(this, error);
    }

    public virtual void AddListener(IConnectionListener listener)
    {
        lock (ConnectionListeners)
        {
            ConnectionListeners.Add(listener);
        }
    }

    public virtual void RemoveListener(IConnectionListener listener)
    {
        lock (ConnectionListeners)
        {
            ConnectionListeners.Remove(listener);
        }

        // TODO: stop maintaining connection?
    }

    public Task WaitForMeadowAttach(CancellationToken? cancellationToken = null)
    {
        throw new NotImplementedException();
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
        else if (command is InitFileWriteRequest fwr)
        {
        }

        _pendingCommands.Enqueue(command);
    }

    private void CommandManager()
    {
        while (!_isDisposed)
        {
            while (_pendingCommands.Count > 0)
            {
                Debug.WriteLine($"There are {_pendingCommands.Count} pending commands");

                var command = _pendingCommands.Dequeue();

                var response = DeliverRequest(command);

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

    protected virtual void Dispose(bool disposing)
    {
        if (!_isDisposed)
        {
            if (disposing)
            {
                //                    Close();
                //                    _port.Dispose();
            }

            _isDisposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}