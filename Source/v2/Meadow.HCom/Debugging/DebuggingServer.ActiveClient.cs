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
        private readonly object _disposeLock = new();

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

            /*_receiveVsDebugDataTask = Task.Run(SendToMeadow, _cts.Token);
            _receiveMeadowDebugDataTask = Task.Run(SendToVisualStudio, _cts.Token);*/
            _receiveVsDebugDataTask = Task.Factory.StartNew(SendToMeadow, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            _receiveMeadowDebugDataTask = Task.Factory.StartNew(SendToVisualStudio, _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void MeadowConnection_DebuggerMessageReceived(object sender, byte[] e)
        {
            lock (_disposeLock)
            {
                if (!_disposed)
                {
                    _debuggerMessages.Add(e);
                }
            }
        }

        private const int RECEIVE_BUFFER_SIZE = 256;

        private async Task SendToMeadow()
        {
            byte[]? receiveBuffer = null;

            try
            {
                using var md5 = MD5.Create();
                receiveBuffer = ArrayPool<byte>.Shared.Rent(RECEIVE_BUFFER_SIZE);
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

                            Debug.WriteLine($"ToMeadow: {BitConverter.ToString(meadowBuffer)}");

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
                _logger?.LogError($"Error receiving data from Visual Studio.{Environment.NewLine}Error: {ex.Message}{Environment.NewLine}StackTrace:{Environment.NewLine}{ex.StackTrace}");
                throw;
            }
            finally
            {
                if (receiveBuffer != null)
                {
                    ArrayPool<byte>.Shared.Return(receiveBuffer);
                }
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
                        if (_debuggerMessages.TryTake(out var byteData, Timeout.Infinite, _cts.Token))
                        {
                            _logger?.LogTrace("Received {count} bytes from Meadow, will forward to VS", byteData.Length);
                            if (!_tcpClient.Connected)
                            {
                                _logger?.LogDebug("Cannot forward data, Visual Studio is not connected");
                                return;
                            }

                            await _networkStream.WriteAsync(byteData, 0, byteData.Length, _cts.Token);
                            _logger?.LogTrace("Forwarded {count} bytes to VS", byteData.Length);

                            Debug.WriteLine($"ToVisStu: {BitConverter.ToString(byteData)}");
                        }
                        else
                        {
                            // If no _debuggerMessages to Take, delay a bit to avoid busy waiting
                            await Task.Delay(100);
                        }
                    }
                    else
                    {
                        _logger?.LogInformation("Unable to Write Data from Visual Studio");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("Operation Cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error sending data to Visual Studio.{Environment.NewLine}Error: {ex.Message}{Environment.NewLine}StackTrace:{Environment.NewLine}{ex.StackTrace}");

                if (!_cts.Token.IsCancellationRequested)
                {
                    throw;
                }
            }
        }

        public void Dispose()
        {
            lock (_disposeLock)
            {
                if (_disposed) return;

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
}