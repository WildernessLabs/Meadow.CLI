using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Meadow.Hcom
{
    internal class ActiveClient : IDisposable
    {
        private readonly IMeadowConnection _connection;
        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _networkStream;

        private readonly CancellationTokenSource _cts;
        private readonly Task _receiveVsDebugDataTask;
        private readonly Task _receiveMeadowDebugDataTask;
        private readonly ILogger? _logger;
        public bool Disposed = false;
        private readonly BlockingCollection<byte[]> _debuggerMessages = new();

        private const int RECEIVE_BUFFER_SIZE = 256;

        // Constructor
        internal ActiveClient(IMeadowConnection connection, TcpClient tcpClient, ILogger? logger, CancellationToken? cancellationToken)
        {
            if (cancellationToken != null)
            {
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken.Value);
            }
            else
            {
                _cts = new CancellationTokenSource();
            }

            _logger = logger;

            _connection = connection;
            _connection.DebuggerMessageReceived += MeadowConnection_DebuggerMessageReceived;

            _tcpClient = tcpClient;
            _networkStream = tcpClient.GetStream();
            _logger?.LogDebug("Starting receive task");
            _receiveVsDebugDataTask = Task.Factory.StartNew(SendToMeadow, TaskCreationOptions.LongRunning);
            _receiveMeadowDebugDataTask = Task.Factory.StartNew(SendToVisualStudio, TaskCreationOptions.LongRunning);
        }

        private void MeadowConnection_DebuggerMessageReceived(object sender, byte[] e)
        {
            _logger?.LogDebug("Debugger Message Received, Adding to collection");
            _debuggerMessages.Add(e);
        }

        private async Task SendToMeadow()
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

                            await _connection.SendDebuggerData(meadowBuffer, 0, _cts.Token);
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

        private async Task SendToVisualStudio()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    if (_networkStream != null && _networkStream.CanWrite)
                    {
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
                        // User probably hit stop
                        _logger?.LogInformation("Unable to Write Data from Visual Studio");
                    }
                }
            }
            catch (OperationCanceledException oce)
            {
                // User probably hit stop; Removed logging as User doesn't need to see this
                // Keeping it as a TODO in case we find a side effect that needs logging.
                _logger?.LogInformation("Operation Cancelled");
                _logger?.LogTrace(oce, "Operation Cancelled");
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error sending data to Visual Studio.{Environment.NewLine}Error: {ex.Message}{Environment.NewLine}StackTrace:{Environment.NewLine}{ex.StackTrace}");

                if (_cts.IsCancellationRequested)
                {
                    throw;
                }
            }
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

                if (_connection != null)
                {
                    _connection.DebuggerMessageReceived -= MeadowConnection_DebuggerMessageReceived;
                }
                Disposed = true;
            }
        }
    }
}