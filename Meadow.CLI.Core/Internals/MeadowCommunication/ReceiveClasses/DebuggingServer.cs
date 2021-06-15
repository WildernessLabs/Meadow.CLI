using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.Devices;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses
{
    // This TCP server directly interacts with Visual Studio debugging.
    // What it receives from Visual Studio it forwards to Meadow.
    // What it receives from Meadow it forwards to Visual Studio.
    public class DebuggingServer : IDisposable
    {
        // VS 2019 - 4024
        // VS 2017 - 4022
        // VS 2015 - 4020
        public IPEndPoint LocalEndpoint { get; private set; }

        private CancellationTokenSource _cancellationTokenSource;
        private readonly ILogger _logger;
        private readonly IMeadowDevice _meadow;
        private ActiveClient? _activeClient;
        private int _activeClientCount = 0;

        // Constructor
        /// <summary>
        /// Create a new DebuggingServer for proxying debug data between VS and Meadow
        /// </summary>
        /// <param name="meadow">The <see cref="IMeadowDevice"/> to debug</param>
        /// <param name="localEndpoint">The <see cref="IPEndPoint"/> to listen for incoming debugger connections</param>
        /// <param name="logger">The <see cref="ILogger"/> to logging state information</param>
        public DebuggingServer(IMeadowDevice meadow, IPEndPoint localEndpoint, ILogger logger)
        {
            LocalEndpoint = localEndpoint;
            _meadow = meadow;
            _logger = logger;
        }

        public async Task StartListeningAsync(CancellationToken cancellationToken)
        {
            try
            {
                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var tcpListener = new TcpListener(LocalEndpoint);
                tcpListener.Start();
                LocalEndpoint = (IPEndPoint)tcpListener.LocalEndpoint;
                _logger.LogInformation("Listening for Visual Studio to connect on {address}:{port}", LocalEndpoint.Address, LocalEndpoint.Port);

                while (true)
                {
                    // Wait for client to connect
                    TcpClient tcpClient = await tcpListener.AcceptTcpClientAsync();
                    OnConnect(tcpClient);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while listening for debugging connections");
            }
        }

        private void OnConnect(TcpClient tcpClient)
        {
            try
            {
                _logger.LogInformation("Visual Studio has connected");
                lock (this)
                {
                    if (_activeClientCount > 0 && _activeClient?.Disposed == false)
                    {
                        _logger.LogDebug("Closing active client");
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
                _logger.LogError(ex, "An error occurred while listening for debugging connections");
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
            _cancellationTokenSource.Cancel(false);
            _activeClient?.Dispose();
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
            private readonly ILogger _logger;
            public bool Disposed = false;

            // Constructor
            internal ActiveClient(IMeadowDevice meadow, TcpClient tcpClient, ILogger logger, CancellationToken cancellationToken)
            {
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _logger = logger;
                _meadow = meadow;
                _tcpClient = tcpClient;
                _networkStream = tcpClient.GetStream();
                _logger.LogDebug("Starting receive task");
                _receiveVsDebugDataTask = Task.Factory.StartNew(SendToMeadowAsync, TaskCreationOptions.LongRunning);
                _receiveMeadowDebugDataTask = Task.Factory.StartNew(SendToVisualStudio, TaskCreationOptions.LongRunning);
            }

            private async Task SendToMeadowAsync()
            {
                try
                {
                    using var md5 = MD5.Create();
                    // Receive from Visual Studio and send to Meadow
                    var receiveBuffer = ArrayPool<byte>.Shared.Rent(490);
                    var meadowBuffer = Array.Empty<byte>();
                    while (!_cts.IsCancellationRequested)
                    {
                        int bytesRead;

                        read:
                        bytesRead = await _networkStream.ReadAsync(receiveBuffer, 0, receiveBuffer.Length);
                        if (bytesRead == 0 || _cts.IsCancellationRequested)
                            continue;

                        var destIndex = meadowBuffer.Length;
                        Array.Resize(ref meadowBuffer, destIndex + bytesRead);
                        Array.Copy(receiveBuffer, 0, meadowBuffer, destIndex, bytesRead);

                        // Ensure we read all the data in this message before passing it along
                        // I'm not sure this is actually needed, the whole message should get read at once.
                        if (_networkStream.DataAvailable)
                            goto read;

                        // Forward to Meadow
                        _logger.LogTrace("Received {count} bytes from VS will forward to HCOM. {hash}",
                                         meadowBuffer.Length,
                                         BitConverter.ToString(md5.ComputeHash(meadowBuffer))
                                                     .Replace("-", string.Empty)
                                                     .ToLowerInvariant());
                        await _meadow.ForwardVisualStudioDataToMonoAsync(meadowBuffer, 0).ConfigureAwait(false);
                        meadowBuffer = Array.Empty<byte>();
                    }
                }
                catch (IOException ioe)
                {
                    // VS client probably died
                    _logger.LogInformation("Visual Studio Disconnected");
                    _logger.LogTrace(ioe, "Visual Studio Disconnected");
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error receiving data from Visual Studio");
                    throw;
                }
            }

            private async Task SendToVisualStudio()
            {
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        var byteData = _meadow.DataProcessor.DebuggerMessages.Take(_cts.Token);
                        _logger.LogTrace("Forwarding {count} bytes to VS", byteData.Length);
                        if (!_tcpClient.Connected)
                        {
                            _logger.LogDebug("Cannot forward data, Visual Studio is not connected");
                            return;
                        }

                        await _networkStream.WriteAsync(byteData, 0, byteData.Length, _cts.Token);
                        _logger.LogTrace("Forwarded {count} bytes to VS", byteData.Length);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Error sending data to Visual Studio");
                        if (_cts.IsCancellationRequested)
                            throw;
                    }
                }
            }

            public void Dispose()
            {
                lock (_tcpClient)
                {
                    if (Disposed)
                        return;
                    _logger.LogTrace("Disposing ActiveClient");
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
}