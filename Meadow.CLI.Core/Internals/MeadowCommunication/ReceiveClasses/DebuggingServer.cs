using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses
{
    // This TCP server directly interacts with Visual Studio debugging.
    // What it receives from Visual Studio it forwards to Meadow.
    // What it receives from Meadow it forwards to Visual Studio.
    public class DebuggingServer
    {
        // VS 2019 - 4024
        // VS 2017 - 4022
        // VS 2015 - 4020
        private readonly int _vsPort;
        private ActiveClient? _activeClient;
        private int _activeClientCount = 0;
        private readonly ILogger _logger;
        private readonly MeadowDevice _device;

        // Constructor
        public DebuggingServer(MeadowDevice device, int visualStudioPort, ILogger? logger = null)
        {
            _device = device;
            _vsPort = visualStudioPort;
            _logger = logger ?? NullLogger.Instance;
        }

        public async Task StartListening(CancellationToken cancellationToken)
        {
            try
            {
                IPHostEntry ipHostInfo = await Dns.GetHostEntryAsync("localhost");
                IPAddress ipAddress = ipHostInfo.AddressList[0];
                IPEndPoint localEndPoint = new IPEndPoint(ipAddress, _vsPort);

                TcpListener tcpListener = new TcpListener(localEndPoint);
                tcpListener.Start();
                _logger.LogInformation("Listening for Visual Studio to connect");

                while (true)
                {
                    await Task.Run(
                        async () =>
                        {
                            // Wait for client to connect
                            TcpClient tcpClient = await tcpListener.AcceptTcpClientAsync();

                            // tcpClient valid after connection
                            _logger.LogInformation("Visual Studio has connected");
                            if (_activeClientCount > 0)
                            {
                                Debug.Assert(_activeClientCount == 1);
                                Debug.Assert(_activeClient != null);
                                _activeClient?.Close();
                                _activeClient = null;
                                _activeClientCount = 0;
                            }

                            _activeClient = new ActiveClient(this, tcpClient, _logger);
                            _activeClient.ReceiveVsDebug(_device);
                            _activeClientCount++;
                        },
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while listening for data");
            }
        }

        internal void CloseActiveClient()
        {
            _activeClient?.Close();
            _activeClient = null;
            _activeClientCount = 0;
        }

        public async Task SendToVisualStudio(byte[] byteData,
                                             CancellationToken cancellationToken = default)
        {
            if (_activeClient != null)
            {
                await _activeClient.SendToVisualStudio(byteData, cancellationToken)
                                   .ConfigureAwait(false);
            }
            else
            {
                _logger.LogWarning("Data sent to VS with no active listener");
            }
        }

        // Imbedded class
        private class ActiveClient
        {
            private readonly DebuggingServer _debuggingServer;
            private readonly TcpClient _tcpClient;
            private readonly NetworkStream _networkStream;
            private bool _okayToRun;
            private readonly ILogger _logger;

            // Constructor
            internal ActiveClient(DebuggingServer debuggingServer,
                                  TcpClient tcpClient,
                                  ILogger logger)
            {
                _debuggingServer = debuggingServer;
                _okayToRun = true;
                _tcpClient = tcpClient;
                _networkStream = _tcpClient.GetStream();
                _logger = logger;
            }

            internal void Close()
            {
                _logger.LogInformation("ActiveClient:Close active client");
                _okayToRun = false;
                _tcpClient.Close(); // Closes NetworkStream too
            }

            internal async void ReceiveVsDebug(MeadowDevice meadow)
            {
                // Console.WriteLine("ActiveClient:Start receiving from VS");
                try
                {
                    // Receive from Visual Studio and send to Meadow
                    await Task.Run(
                        async () =>
                        {
                            while (_tcpClient.Connected && _okayToRun)
                            {
                                var receiveBuffer = new byte[490];
                                var bytesRead = await _networkStream.ReadAsync(
                                                    receiveBuffer,
                                                    0,
                                                    receiveBuffer.Length);

                                if (!_okayToRun)
                                    break;

                                if (bytesRead > 0)
                                {
                                    _logger.LogTrace(
                                        $"Received {bytesRead} bytes from VS will forward to Meadow");

                                    // Need a buffer the exact size of received data to work with CLI
                                    var meadowBuffer = new byte[bytesRead];
                                    Array.Copy(receiveBuffer, 0, meadowBuffer, 0, bytesRead);

                                    // Forward to Meadow
                                    await meadow.ForwardVisualStudioDataToMonoAsync(meadowBuffer, 0)
                                                .ConfigureAwait(false);

                                    _logger.LogTrace(
                                        $"Forwarded {bytesRead} bytes from VS will forward to Meadow");
                                }
                            }
                        });
                }
                catch (IOException ioe)
                {
                    // VS client probably died
                    _logger.LogError(ioe, "An error occurred while communicating with VS");
                    _debuggingServer.CloseActiveClient();
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "An error occurred while communicating with VS");
                    _debuggingServer.CloseActiveClient();
                    if (_okayToRun)
                        throw;
                }
            }

            public async Task SendToVisualStudio(byte[] byteData,
                                                 CancellationToken cancellationToken = default)
            {
                try
                {
                    _logger.LogTrace($"Forwarding {byteData.Length} bytes to VS");
                    // Receive from Meadow and send to Visual Studio
                    if (!_tcpClient.Connected)
                    {
                        _logger.LogDebug(
                            "Send attempt is not possible, Visual Studio not connected");

                        return;
                    }

                    await _networkStream.WriteAsync(byteData, 0, byteData.Length, cancellationToken)
                                        .ConfigureAwait(false);

                    _logger.LogTrace($"Forwarded {byteData.Length} bytes to VS");
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "An error occurred while communicating with VS");
                    if (_okayToRun)
                        throw;
                }
            }
        }
    }
}