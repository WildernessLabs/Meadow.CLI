using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.DeviceManagement;
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

        private readonly ILogger _logger;
        private readonly MeadowDevice _meadow;
        private ActiveClient? _activeClient;
        private int _activeClientCount = 0;

        // Constructor
        public DebuggingServer(MeadowDevice meadow, IPEndPoint localEndpoint, ILogger logger)
        {
            LocalEndpoint = localEndpoint;
            _meadow = meadow;
            _logger = logger;
        }

        public async Task StartListeningAsync()
        {
            try
            {
                var tcpListener = new TcpListener(LocalEndpoint);
                tcpListener.Start();
                LocalEndpoint = (IPEndPoint)tcpListener.LocalEndpoint;
                Console.WriteLine("Listening for Visual Studio to connect");

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

        void OnConnect(TcpClient tcpClient)
        {
            try
            {
                _logger.LogInformation("Visual Studio has connected");
                lock (this)
                {
                    if (_activeClientCount > 0 && _activeClient?.Disposed == false)
                    {
                        Debug.Assert(_activeClientCount == 1);
                        Debug.Assert(_activeClient != null);
                        CloseActiveClient();
                    }

                    _activeClient = new ActiveClient(_meadow, tcpClient, _logger);
                    _activeClientCount++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        internal void CloseActiveClient()
        {
            _activeClient?.Dispose();
            _activeClient = null;
            _activeClientCount = 0;
        }

        public Task SendToVisualStudio(byte[] byteData)
        {
            return _activeClient != null ? _activeClient.SendToVisualStudio(byteData) : Task.CompletedTask;
        }

        public void Dispose()
        {
            _activeClient?.Dispose();
        }

        // Embedded class
        private class ActiveClient : IDisposable
        {
            private readonly MeadowDevice _meadow;
            private readonly TcpClient _tcpClient;
            private readonly NetworkStream _networkStream;
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();
            //private readonly Task _receiverTask;
            //private readonly Task _pusherTask;
            private readonly Task _receiveVsDebugTask;
            private readonly ILogger _logger;
            public bool Disposed = false;

            // Constructor
            internal ActiveClient(MeadowDevice meadow, TcpClient tcpClient, ILogger logger)
            {
                _logger = logger;
                _meadow = meadow;
                _tcpClient = tcpClient;
                _networkStream = tcpClient.GetStream();
                _receiveVsDebugTask = Task.Factory.StartNew(
                    ReceiveVsDebug,
                    TaskCreationOptions.LongRunning);
                //var pipe = new Pipe();
                //_receiverTask = Task.Factory.StartNew(() => 
                //    ReadDataFromMeadow(_networkStream, pipe.Writer, _cts.Token),
                //    TaskCreationOptions.LongRunning);

                //_pusherTask = Task.Factory.StartNew(() => PushDataToVs(pipe.Reader, _cts.Token), TaskCreationOptions.LongRunning);
            }

            // TODO: Finish implementing pipe
            private async Task ReadDataFromMeadow(Stream stream, PipeWriter writer, CancellationToken cancellationToken)
            {
                const int minimumBufferSize = 1024;
                while (!cancellationToken.IsCancellationRequested)
                {
                    // Allocate at least 512 bytes from the PipeWriter
                    var memory = writer.GetMemory(minimumBufferSize);
                    try 
                    {
                        var bytesRead = await stream.ReadAsync(memory, cancellationToken).ConfigureAwait(false);
                        if (bytesRead == 0)
                        {
                            break;
                        }
                        // Tell the PipeWriter how much was read from the Socket
                        writer.Advance(bytesRead);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "foo");
                        break;
                    }

                    // Make the data available to the PipeReader
                    var result = await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
            }

            private async Task PushDataToVs(PipeReader reader, CancellationToken cancellationToken)
            {
                while (true)
                {
                    var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);

                    var buffer = result.Buffer;
                    SequencePosition? position = null;

                    do 
                    {
                        // Look for a EOL in the buffer

                        if (position != null)
                        {
                            //await _meadow.ForwardVisualStudioDataToMonoAsync(buffer)
                
                            // Skip the line + the \n character (basically position)
                            buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
                        }
                    }
                    while (position != null);

                    // Tell the PipeReader how much of the buffer we have consumed
                    reader.AdvanceTo(buffer.Start, buffer.End);

                    // Stop reading if there's no more data coming
                    if (result.IsCompleted)
                    {
                        break;
                    }
                }

                // Mark the PipeReader as complete
                await reader.CompleteAsync().ConfigureAwait(false);
            }

            private async Task ReceiveVsDebug()
            {
                // Console.WriteLine("ActiveClient:Start receiving from VS");
                try
                {
                    // Receive from Visual Studio and send to Meadow
                    var ct = _cts.Token;
                    while (_tcpClient.Connected && !_cts.IsCancellationRequested)
                    {
                        var meadowBuffer = Array.Empty<byte>();
                        while (_networkStream.DataAvailable)
                        {
                            var receivedBuffer = ArrayPool<byte>.Shared.Rent(1024);
                            var bytesRead = await _networkStream
                                              .ReadAsync(receivedBuffer, 0, receivedBuffer.Length, ct)
                                              .ConfigureAwait(false);

                            if (bytesRead == 0 || !_cts.IsCancellationRequested)
                                continue;

                            var destIndex = meadowBuffer.Length;
                            Array.Resize(ref meadowBuffer, destIndex + bytesRead);
                            Array.Copy(receivedBuffer, 0, meadowBuffer, destIndex, bytesRead);
                        }

                        _logger.LogTrace("Received {count} bytes from VS will forward to HCOM", meadowBuffer.Length);

                        
                        // Forward to Meadow
                        await _meadow.ForwardVisualStudioDataToMonoAsync(meadowBuffer, 0, ct)
                                     .ConfigureAwait(false);

                        _logger.LogTrace("Forwarded {count} bytes from VS to HCOM", meadowBuffer.Length);
                    }
                }
                catch (IOException ioe)
                {
                    // VS client probably died
                    _logger.LogError(ioe, "Error forwarding data to Mono");
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error forwarding data to Mono");
                }
            }

            public async Task SendToVisualStudio(byte[] byteData)
            {
                _logger.LogTrace("Forwarding {count} bytes to VS", byteData.Length);
                try
                {
                    // Receive from Meadow and send to Visual Studio
                    if (!_tcpClient.Connected)
                    {
                        _logger.LogDebug("Cannot forward data, Visual Studio is not connected");
                        return;
                    }

                    await _networkStream.WriteAsync(byteData, 0, byteData.Length);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error sending data to Visual Studio");
                    if (_cts.IsCancellationRequested)
                        throw;
                }
            }

            public void Dispose()
            {
                lock (_tcpClient)
                {
                    if (Disposed)
                        return;
                    _tcpClient.Dispose();
                    _networkStream.Dispose();
                    _cts.Dispose();
                    //_receiverTask?.Dispose();
                    //_pusherTask?.Dispose();
                    _receiveVsDebugTask?.Dispose();
                    Disposed = true;
                }
            }
        }
    }
}