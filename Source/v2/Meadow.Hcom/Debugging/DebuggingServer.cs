using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

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
    private CancellationTokenSource _cancellationTokenSource;
    private readonly ILogger? _logger;
    private readonly IMeadowDevice _meadow;
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
    public DebuggingServer(IMeadowDevice meadow, IPEndPoint localEndpoint, ILogger? logger)
    {
        LocalEndpoint = localEndpoint;
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

                _activeClient = new ActiveClient(_meadow, tcpClient, _logger, _cancellationTokenSource.Token);
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

    // Embedded class
    private class ActiveClient : IDisposable
    {
        private readonly IMeadowDevice _meadow;
        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _networkStream;

        private readonly CancellationTokenSource _cts;
        private readonly Task _receiveVsDebugDataTask;
        private readonly Task _receiveMeadowDebugDataTask;
        private readonly ILogger? _logger;
        public bool Disposed = false;

        // Constructor
        internal ActiveClient(IMeadowDevice meadow, TcpClient tcpClient, ILogger? logger, CancellationToken cancellationToken)
        {
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _logger = logger;
            _meadow = meadow;
            _tcpClient = tcpClient;
            _networkStream = tcpClient.GetStream();
            _logger?.LogDebug("Starting receive task");
            _receiveVsDebugDataTask = Task.Factory.StartNew(SendToMeadowAsync, TaskCreationOptions.LongRunning);
            _receiveMeadowDebugDataTask = Task.Factory.StartNew(SendToVisualStudio, TaskCreationOptions.LongRunning);
        }

        private const int RECEIVE_BUFFER_SIZE = 256;

        private async Task SendToMeadowAsync()
        {
            try
            {
                using var md5 = MD5.Create();
                // Receive from Visual Studio and send to Meadow
                var receiveBuffer = ArrayPool<byte>.Shared.Rent(RECEIVE_BUFFER_SIZE);
                var meadowBuffer = Array.Empty<byte>();

                while (!_cts.IsCancellationRequested)
                {
                    if (_networkStream != null && _networkStream.CanRead)
                    {
                        int bytesRead;
                        do
                        {
                            bytesRead = await _networkStream.ReadAsync(receiveBuffer, 0, receiveBuffer.Length, _cts.Token);

                            if (bytesRead == 0 || _cts.IsCancellationRequested)
                            {
                                continue;
                            }

                            var destIndex = meadowBuffer.Length;
                            Array.Resize(ref meadowBuffer, destIndex + bytesRead);
                            Array.Copy(receiveBuffer, 0, meadowBuffer, destIndex, bytesRead);

                            // Forward the RECIEVE_BUFFER_SIZE chunk to Meadow immediately
                            _logger?.LogTrace("Received {count} bytes from VS, will forward to HCOM/Meadow. {hash}",
                                                meadowBuffer.Length,
                                                BitConverter.ToString(md5.ComputeHash(meadowBuffer))
                                                            .Replace("-", string.Empty)
                                                            .ToLowerInvariant());

                            await _meadow.SendDebuggerData(meadowBuffer, 0, _cts.Token);
                            meadowBuffer = Array.Empty<byte>();

                            // Ensure we read all the data in this message before passing it along
                            // I'm not sure this is actually needed, the whole message should get read at once.
                        } while (_networkStream.DataAvailable);
                    }
                    else
                    {
                        // User probably hit stop
                        _logger?.LogInformation("Unable to Read Data from Visual Studio");
                        _logger?.LogTrace("Unable to Read Data from Visual Studio");
                    }
                }
            }
            catch (IOException ioe)
            {
                // VS client probably died
                _logger?.LogInformation("Visual Studio has Disconnected" + Environment.NewLine);
                _logger?.LogTrace(ioe, "Visual Studio has Disconnected");
            }
            catch (ObjectDisposedException ode)
            {
                // User probably hit stop
                _logger?.LogInformation("Visual Studio has stopped debugging" + Environment.NewLine);
                _logger?.LogTrace(ode, "Visual Studio has stopped debugging");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error receiving data from Visual Studio.{Environment.NewLine}Error: {ex.Message}{Environment.NewLine}StackTrace:{Environment.NewLine}{ex.StackTrace}");
                throw;
            }
        }

        private Task SendToVisualStudio()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    if (_networkStream != null && _networkStream.CanWrite)
                    {
                        /* TODO while (_meadow.DataProcessor.DebuggerMessages.Count > 0)
                        {
                            var byteData = _meadow.DataProcessor.DebuggerMessages.Take(_cts.Token);
                            _logger?.LogTrace("Received {count} bytes from Meadow, will forward to VS", byteData.Length);
                            if (!_tcpClient.Connected)
                            {
                                _logger?.LogDebug("Cannot forward data, Visual Studio is not connected");
                                return;
                            }

                            await _networkStream.WriteAsync(byteData, 0, byteData.Length, _cts.Token);
                            _logger?.LogTrace("Forwarded {count} bytes to VS", byteData.Length);
                        }*/
                    }
                    else
                    {
                        // User probably hit stop
                        _logger?.LogInformation("Unable to Write Data from Visual Studio");
                        _logger?.LogTrace("Unable to Write Data from Visual Studio");
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                // User probably hit stop; Removed logging as User doesn't need to see this
                // Keeping it as a TODO in case we find a side effect that needs logging.
                // TODO _logger?.LogInformation("Operation Cancelled");
                // TODO _logger?.LogTrace(oce, "Operation Cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error sending data to Visual Studio.{Environment.NewLine}Error: {ex.Message}{Environment.NewLine}StackTrace:{Environment.NewLine}{ex.StackTrace}");

                if (_cts.IsCancellationRequested)
                {
                    throw;
                }
            }
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            lock (_tcpClient)
            {
                if (Disposed)
                {
                    return;
                }

                _logger?.LogTrace("Disposing ActiveClient");
                _cts.Cancel(false);
                _receiveVsDebugDataTask.Wait(TimeSpan.FromSeconds(10));
                _receiveMeadowDebugDataTask.Wait(TimeSpan.FromSeconds(10));
                _receiveVsDebugDataTask?.Dispose();
                _receiveMeadowDebugDataTask?.Dispose();
                _tcpClient.Dispose();
                _networkStream.Dispose();
                _cts.Dispose();
                Disposed = true;
            }
        }
    }
}