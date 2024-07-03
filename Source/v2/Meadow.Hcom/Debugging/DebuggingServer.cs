using System.Net;
using System.Net.Sockets;

namespace Meadow.Hcom;

// This TCP server directly interacts with Visual Studio debugging.
// What it receives from Visual Studio it forwards to Meadow.
// What it receives from Meadow it forwards to Visual Studio.
public partial class DebuggingServer : IDisposable
{
    // VS 2019 - 4024
    // VS 2017 - 4022
    // VS 2015 - 4020
    private readonly object _lck = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly ILogger? _logger;
    private readonly IMeadowConnection _connection;
    private ActiveClient? _activeClient;
    private readonly TcpListener _listener;
    private readonly Task? _listenerTask;
    public bool Disposed;

    // Constructor
    /// <summary>
    /// Create a new DebuggingServer for proxying debug data between VS and Meadow
    /// </summary>
    /// <param name="connection">The <see cref="IMeadowConnection"/>meadow connection</param>
    /// <param name="localEndpoint">The <see cref="IPEndPoint"/> to listen for incoming debugger connections</param>
    /// <param name="logger">The <see cref="ILogger"/> to logging state information</param>
    public DebuggingServer(IMeadowConnection connection, int port, ILogger? logger)
    {
        _logger = logger;
        _connection = connection;

        var endPoint = new IPEndPoint(IPAddress.Loopback, port);

        _listener = new TcpListener(endPoint);
    }

    /// <summary>
    /// Start the debugging server
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that is linked internally to the running task</param>
    /// <returns>A <see cref="Task"/> representing the startup operation</returns>
    public async Task StartListening(CancellationToken cancellationToken)
    {
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _listener.Start();
        _logger?.LogInformation($"Listening for Visual Studio to connect");

        // This call will wait for the client to connect, before continuing.
        var tcpClient = await _listener.AcceptTcpClientAsync();
        _activeClient = CreateActiveClient(tcpClient);
    }

    /// <summary>
    /// Stop the <see cref="DebuggingServer"/>
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the shutdown operation</returns>
    public async Task StopListening()
    {
        _listener?.Stop();

        _cancellationTokenSource?.Cancel(false);

        if (_listenerTask != null)
        {
            await _listenerTask;
        }
    }

    private ActiveClient? CreateActiveClient(TcpClient tcpClient)
    {
        try
        {
            lock (_lck)
            {
                _logger?.LogInformation("Visual Studio has Connected" + Environment.NewLine);

                if (_activeClient != null)
                {
                    _logger?.LogDebug("Closing active client");
                    _activeClient?.Dispose();
                    _activeClient = null;
                }

                return new ActiveClient(_connection, tcpClient, _logger, _cancellationTokenSource?.Token);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "An error occurred while connecting to Visual Studio");
        }
        return null;
    }

    public void Dispose()
    {
        lock (_lck)
        {
            if (Disposed)
            {
                return;
            }
            _cancellationTokenSource?.Cancel(false);
            _activeClient?.Dispose();
            _listenerTask?.Dispose();
            Disposed = true;
        }
    }
}