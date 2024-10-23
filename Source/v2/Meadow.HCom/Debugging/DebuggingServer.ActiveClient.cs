using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Meadow.Hcom;

public partial class DebuggingServer
{
    private class ActiveClient : IDisposable
    {
        private readonly IMeadowConnection _connection;
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private readonly CancellationTokenSource _cts;
        private Task _receiveVsDebugDataTask;
        private Task _receiveMeadowDebugDataTask;
        private readonly ILogger? _logger;
        private bool _disposed;
        private readonly BlockingCollection<byte[]> _debuggerMessages = new();

        internal ActiveClient(IMeadowConnection connection, ILogger? logger, CancellationToken? cancellationToken)
        {
            _cts = cancellationToken != null
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Value)
                : new CancellationTokenSource();

            _logger = logger;
            _connection = connection;
            _connection.DebuggerMessageReceived += MeadowConnection_DebuggerMessageReceived;
        }

        public async Task Start(TcpListener tcpListener)
        {
            _tcpClient = await tcpListener.AcceptTcpClientAsync();
            _networkStream = _tcpClient.GetStream();

            _logger?.LogDebug("Starting receive task");
            _receiveVsDebugDataTask = Task.Factory.StartNew(SendToMeadowAsync, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            _receiveMeadowDebugDataTask = Task.Factory.StartNew(SendToVisualStudio, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void MeadowConnection_DebuggerMessageReceived(object sender, byte[] e)
        {
            _debuggerMessages.Add(e);
        }

        private const int RECEIVE_BUFFER_SIZE = 256;

        private async Task SendToMeadowAsync()
        {
            try
            {
                var receiveBuffer = ArrayPool<byte>.Shared.Rent(RECEIVE_BUFFER_SIZE);

                while (!_cts.Token.IsCancellationRequested)
                {
                    if (_networkStream != null && _networkStream.CanRead)
                    {
                        int bytesRead;
                        do
                        {
                            bytesRead = await _networkStream.ReadAsync(receiveBuffer, 0, receiveBuffer.Length, _cts.Token);

                            if (bytesRead == 0 || _cts.Token.IsCancellationRequested)
                            {
                                continue;
                            }

                            var meadowBuffer = new byte[bytesRead];
                            Array.Copy(receiveBuffer, 0, meadowBuffer, 0, bytesRead);

                            _logger?.LogTrace("Received {count} bytes from VS, will forward to HCOM/Meadow. {hash}",
                                                meadowBuffer.Length,
                                                BitConverter.ToString(meadowBuffer)
                                                            .Replace("-", string.Empty)
                                                            .ToLowerInvariant());

                            await _connection.SendDebuggerData(meadowBuffer, 0, _cts.Token);

                            Debug.WriteLine($"ToMeadow: {BitConverter.ToString(meadowBuffer)}");

                        } while (_networkStream.DataAvailable);
                    }
                    else
                    {
                        _logger?.LogInformation("Unable to Read Data from Visual Studio");
                        _logger?.LogTrace("Unable to Read Data from Visual Studio");
                    }
                }
            }
            catch (IOException ioe)
            {
                _logger?.LogInformation("Visual Studio has Disconnected");
            }
            catch (ObjectDisposedException ode)
            {
                _logger?.LogInformation("Visual Studio has stopped debugging");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error receiving data from Visual Studio.\nError: {ex.Message}\nStackTrace:\n{ex.StackTrace}");
                throw;
            }
        }

        private async Task SendToVisualStudio()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    byte[] byteData = null;
                    if (!_debuggerMessages.TryTake(out byteData, 500, _cts.Token))
                    {
                        Console.WriteLine("No data from Meadow");
                        continue;
                    }

                    _logger?.LogTrace("Received {count} bytes from Meadow, will forward to VS", byteData.Length);
                    if (!_tcpClient.Connected || _networkStream == null || !_networkStream.CanWrite)
                    {
                        _logger?.LogDebug("Cannot forward data, Visual Studio is not connected");
                        break;
                    }

                    await _networkStream.WriteAsync(byteData, 0, byteData.Length, _cts.Token);
                    _logger?.LogTrace("Forwarded {count} bytes to VS", byteData.Length);
                    Debug.WriteLine($"ToVisStu: {BitConverter.ToString(byteData)}");
                }
            }
            catch (OperationCanceledException oce)
            {
                _logger?.LogInformation("Operation Cancelled");
                _logger?.LogTrace(oce, "Operation Cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error sending data to Visual Studio.\nError: {ex.Message}\nStackTrace:\n{ex.StackTrace}");

                if (!_cts.Token.IsCancellationRequested)
                {
                    throw;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _logger?.LogTrace("Disposing ActiveClient");
            _cts.Cancel();
            try
            {
                Task.WhenAll(_receiveVsDebugDataTask, _receiveMeadowDebugDataTask).Wait(TimeSpan.FromSeconds(10));
            }
            catch (AggregateException ex)
            {
                _logger?.LogError("Error waiting for tasks to complete during dispose", ex);
            }
            _tcpClient.Dispose();
            _networkStream.Dispose();
            _cts.Dispose();

            if (_connection != null)
            {
                _connection.DebuggerMessageReceived -= MeadowConnection_DebuggerMessageReceived;
            }
            _disposed = true;
        }
    }
}
