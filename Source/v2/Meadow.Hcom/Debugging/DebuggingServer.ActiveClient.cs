using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Meadow.Hcom;

public partial class DebuggingServer
{
    private class ActiveClient : IDisposable
    {
        private readonly IMeadowConnection _connection;
        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _networkStream;
        private readonly CancellationTokenSource _cts;
        private readonly Task _receiveVsDebugDataTask;
        private readonly Task _receiveMeadowDebugDataTask;
        private readonly ILogger? _logger;
        private bool _disposed;
        private readonly BlockingCollection<byte[]> _debuggerMessages = new();
        private readonly AutoResetEvent _vsDebugDataReady = new(false);

        internal ActiveClient(IMeadowConnection connection, TcpClient tcpClient, ILogger? logger, CancellationToken? cancellationToken)
        {
            _cts = cancellationToken != null
                ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Value)
                : new CancellationTokenSource();

            _logger = logger;
            _connection = connection;
            _tcpClient = tcpClient;
            _networkStream = tcpClient.GetStream();

            _logger?.LogDebug("Starting receive task");

            _connection.DebuggerMessageReceived += MeadowConnection_DebuggerMessageReceived;

            _receiveVsDebugDataTask = Task.Factory.StartNew(SendToMeadowAsync, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            _receiveMeadowDebugDataTask = Task.Factory.StartNew(SendToVisualStudio, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void MeadowConnection_DebuggerMessageReceived(object sender, byte[] e)
        {
            _debuggerMessages.Add(e);
            _vsDebugDataReady.Set();
        }

        private const int RECEIVE_BUFFER_SIZE = 256;

        private async Task SendToMeadowAsync()
        {
            try
            {
                using var md5 = MD5.Create();
                var receiveBuffer = ArrayPool<byte>.Shared.Rent(RECEIVE_BUFFER_SIZE);
                var meadowBuffer = Array.Empty<byte>();

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

                            var destIndex = meadowBuffer.Length;
                            Array.Resize(ref meadowBuffer, destIndex + bytesRead);
                            Array.Copy(receiveBuffer, 0, meadowBuffer, destIndex, bytesRead);

                            _logger?.LogTrace("Received {count} bytes from VS, will forward to HCOM/Meadow. {hash}",
                                                meadowBuffer.Length,
                                                BitConverter.ToString(md5.ComputeHash(meadowBuffer))
                                                            .Replace("-", string.Empty)
                                                            .ToLowerInvariant());

                            await _connection.SendDebuggerData(meadowBuffer, 0, _cts.Token);
                            meadowBuffer = Array.Empty<byte>();
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
                _logger?.LogTrace(ioe, "Visual Studio has Disconnected");
            }
            catch (ObjectDisposedException ode)
            {
                _logger?.LogInformation("Visual Studio has stopped debugging");
                _logger?.LogTrace(ode, "Visual Studio has stopped debugging");
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
                while (!_cts.Token.IsCancellationRequested)
                {
                    if (_networkStream != null && _networkStream.CanWrite)
                    {
                        _vsDebugDataReady.WaitOne();

                        while (_debuggerMessages.Count > 0)
                        {
                            var byteData = _debuggerMessages.Take(_cts.Token);

                            _logger?.LogTrace("Received {count} bytes from Meadow, will forward to VS", byteData.Length);
                            if (!_tcpClient.Connected)
                            {
                                _logger?.LogDebug("Cannot forward data, Visual Studio is not connected");
                                return;
                            }

                            await _networkStream.WriteAsync(byteData, 0, byteData.Length, _cts.Token);
                            _logger?.LogTrace("Forwarded {count} bytes to VS", byteData.Length);
                        }
                    }
                    else
                    {
                        _logger?.LogInformation("Unable to Write Data from Visual Studio");
                    }
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
