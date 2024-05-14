using System.Net;
using System.Net.Sockets;

namespace Meadow.Hcom;

// This TCP server directly interacts with Visual Studio debugging.
// What it receives from Visual Studio it forwards to Meadow.
// What it receives from Meadow it forwards to Visual Studio.
public class DebuggingServer : IDisposable
{
    // VS 2019 - 4024
    // VS 2017 - 4022
    // VS 2015 - 4020
    public IPEndPoint LocalEndpoint { get; private set; }
    private readonly object _lck = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly ILogger? _logger;
    private readonly IMeadowDevice _meadow;
    private readonly IMeadowConnection _connection;
    private ActiveClient? _activeClient;
    private int _activeClientCount = 0;
    private readonly TcpListener _listener;
    private Task? _listenerTask;
    private bool _isReady;
    public bool Disposed;

    // Constructor
    /// <summary>
    /// Create a new DebuggingServer for proxying debug data between VS and Meadow
    /// </summary>
    /// <param name="meadow">The <see cref="IMeadowDevice"/> to debug</param>
    /// <param name="localEndpoint">The <see cref="IPEndPoint"/> to listen for incoming debugger connections</param>
    /// <param name="logger">The <see cref="ILogger"/> to logging state information</param>
    public DebuggingServer(IMeadowConnection connection, IMeadowDevice meadow, IPEndPoint localEndpoint, ILogger? logger)
    {
        LocalEndpoint = localEndpoint;
        _connection = connection;

        _meadow = meadow;
        _logger = logger;
        _listener = new TcpListener(LocalEndpoint);
    }

    /// <summary>
    /// Start the debugging server
    /// </summary>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> that is linked internally to the running task</param>
    /// <returns>A <see cref="Task"/> representing the startup operation</returns>
    public async Task StartListening(CancellationToken cancellationToken)
    {
        if (cancellationToken != null)
        {
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _listenerTask = Task.Factory.StartNew(StartListener, TaskCreationOptions.LongRunning);
            var startTimeout = DateTime.UtcNow.AddSeconds(60);
            while (DateTime.UtcNow < startTimeout)
            {
                if (_isReady)
                {
                    return;
                }

                await Task.Delay(100, cancellationToken);
            }

            throw new Exception("DebuggingServer did not start listening within the 60 second timeout.");
        }
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

    private async Task StartListener()
    {
        try
        {
            _listener.Start();
            LocalEndpoint = (IPEndPoint)_listener.LocalEndpoint;
            _logger?.LogInformation($"Listening for Visual Studio to connect on {LocalEndpoint.Address}:{LocalEndpoint.Port}" + Environment.NewLine);
            _isReady = true;

            // This call will wait for the client to connect, before continuing. We shouldn't need a loop.
            TcpClient tcpClient = await _listener.AcceptTcpClientAsync();
            OnConnect(tcpClient);
        }
        catch (SocketException soex)
        {
            _logger?.LogError("A Socket error occurred. The port may already be in use. Try rebooting to free up the port.");
            _logger?.LogError($"Error:\n{soex.Message} \nStack Trace:\n{soex.StackTrace}");
        }
        catch (Exception ex)
        {
            _logger?.LogError("An unhandled exception occurred while listening for debugging connections.");
            _logger?.LogError($"Error:\n{ex.Message} \nStack Trace:\n{ex.StackTrace}");
        }
    }

    private void OnConnect(TcpClient tcpClient)
    {
        try
        {
            lock (_lck)
            {
                _logger?.LogInformation("Visual Studio has Connected" + Environment.NewLine);
                if (_activeClientCount > 0 && _activeClient?.Disposed == false)
                {
                    _logger?.LogDebug("Closing active client");
                    Debug.Assert(_activeClientCount == 1);
                    Debug.Assert(_activeClient != null);
                    CloseActiveClient();
                }

                _activeClient = new ActiveClient(_connection, tcpClient, _logger, _cancellationTokenSource?.Token);
                _activeClientCount++;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "An error occurred while connecting to Visual Studio");
        }
    }

    internal void CloseActiveClient()
    {
        _activeClient?.Dispose();
        _activeClient = null;
        _activeClientCount = 0;
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