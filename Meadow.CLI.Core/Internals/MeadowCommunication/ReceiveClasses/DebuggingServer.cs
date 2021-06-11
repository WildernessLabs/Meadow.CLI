using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
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

        private readonly CancellationTokenSource _cancellationTokenSource =
            new CancellationTokenSource();
        private readonly ILogger _logger;
        private readonly MeadowDevice _meadow;
        private readonly IList<byte[]> _pendingMessages = new List<byte[]>();
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
                        Debug.Assert(_activeClientCount == 1);
                        Debug.Assert(_activeClient != null);
                        CloseActiveClient();
                    }

                    _activeClient = new ActiveClient(_meadow, tcpClient, _logger, _pendingMessages, _cancellationTokenSource.Token);
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

        public async Task SendToVisualStudio(byte[] byteData, CancellationToken cancellationToken)
        {
            if (_activeClient == null)
            {
                _logger.LogDebug("Storing debugger data for Visual Studio");
                _pendingMessages.Add(byteData);
            }
            else
            {
                if (_pendingMessages.Any())
                {
                    _logger.LogDebug("Flushing pending debugger messages.");
                    foreach (var pendingMessage in _pendingMessages)
                    {
                        await _activeClient.SendToVisualStudio(pendingMessage, cancellationToken).ConfigureAwait(false);
                    }
                    _pendingMessages.Clear();
                }

                await _activeClient.SendToVisualStudio(byteData, cancellationToken).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel(false);
            _activeClient?.Dispose();
        }

        // Embedded class
        private class ActiveClient : IDisposable
        {
            private readonly MeadowDevice _meadow;
            private readonly TcpClient _tcpClient;
            private readonly NetworkStream _networkStream;

            private readonly CancellationTokenSource _cts;
            private readonly Task _receiverTask;
            private readonly Task _pusherTask;
            //private readonly Task _receiveVsDebugTask;
            private readonly ILogger _logger;
            public bool Disposed = false;

            // Constructor
            internal ActiveClient(MeadowDevice meadow, TcpClient tcpClient, ILogger logger, IList<byte[]> pendingMessages, CancellationToken cancellationToken)
            {
                _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _logger = logger;
                _meadow = meadow;
                _tcpClient = tcpClient;
                _networkStream = tcpClient.GetStream();
                foreach (var pendingMessage in pendingMessages)
                {
                    _networkStream.Write(pendingMessage);
                }
                pendingMessages.Clear();
                _logger.LogDebug("Starting receive task");
                //_receiveVsDebugTask = Task.Factory.StartNew(
                //    ReceiveVsDebug,
                //    TaskCreationOptions.LongRunning);
                var pipe = new Pipe();
                _receiverTask = Task.Factory.StartNew(() =>
                    ReadDataFromVisualStudio(_networkStream, pipe.Writer, _cts.Token),
                    TaskCreationOptions.LongRunning);

                _pusherTask = Task.Factory.StartNew(() => PushDataToMeadow(pipe.Reader, _cts.Token), TaskCreationOptions.LongRunning);
            }

            // TODO: Finish implementing pipe
            private async Task ReadDataFromVisualStudio(Stream stream, PipeWriter writer, CancellationToken cancellationToken)
            {
                try
                {
                    const int minimumBufferSize = 1024;
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        // Allocate at least 512 bytes from the PipeWriter
                        var memory = writer.GetMemory(minimumBufferSize);
                        try
                        {
                            var bytesRead = await stream.ReadAsync(memory, cancellationToken)
                                                        .ConfigureAwait(false);

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
                        var result = await writer.FlushAsync(cancellationToken)
                                                 .ConfigureAwait(false);

                        if (result.IsCompleted)
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, ":-(");
                    throw;
                }
            }

            private async Task PushDataToMeadow(PipeReader reader, CancellationToken cancellationToken)
            {
                var first = true;
                using var md5 = MD5.Create();
                while (true)
                {
                    var result = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);

                    var buffer = result.Buffer;
                    foreach (var segment in buffer)
                    {
                        if (segment.IsEmpty) continue;
                        var sequence = segment.ToArray();
                        if (first)
                        {
                            var tmp = sequence[..13];
                            _logger.LogTrace("Received {count} bytes from VS will forward to HCOM. {hash}", tmp.Length, BitConverter.ToString(md5.ComputeHash(tmp)).Replace("-", string.Empty).ToLowerInvariant());
                            await _meadow.ForwardVisualStudioDataToMonoAsync(tmp, 0, _cts.Token).ConfigureAwait(false);
                            tmp = sequence[13..];
                            _logger.LogTrace("Received {count} bytes from VS will forward to HCOM. {hash}", tmp.Length, BitConverter.ToString(md5.ComputeHash(tmp)).Replace("-", string.Empty).ToLowerInvariant());
                            await _meadow.ForwardVisualStudioDataToMonoAsync(tmp, 0, _cts.Token).ConfigureAwait(false);
                            first = false;
                        }
                        else
                        {
                            _logger.LogTrace("Received {count} bytes from VS will forward to HCOM. {hash}", sequence.Length, BitConverter.ToString(md5.ComputeHash(sequence)).Replace("-", string.Empty).ToLowerInvariant());
                            await _meadow.ForwardVisualStudioDataToMonoAsync(sequence, 0, _cts.Token).ConfigureAwait(false);
                        }
                        
                        
                    }
                    
                    // Tell the PipeReader how much of the buffer we have consumed
                    reader.AdvanceTo(consumed: buffer.End);

                    // Stop reading if there's no more data coming
                    if (result.IsCompleted)
                    {
                        break;
                    }
                }

                // Mark the PipeReader as complete
                await reader.CompleteAsync().ConfigureAwait(false);
            }

            public async Task SendToVisualStudio(byte[] byteData, CancellationToken cancellationToken)
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

                    await _networkStream.WriteAsync(byteData, 0, byteData.Length, cancellationToken);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error sending data to Visual Studio");
                    if (_cts.IsCancellationRequested)
                        throw;
                }
                _logger.LogTrace("Forwarded {count} bytes to VS", byteData.Length);
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
                    _receiverTask?.Dispose();
                    _pusherTask?.Dispose();
                    //_receiveVsDebugTask?.Dispose();
                    Disposed = true;
                }
            }
        }
    }
}