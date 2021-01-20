using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;  
using System.Net.Sockets;  
using System.Threading;
using System.Threading.Tasks;
using MeadowCLI.DeviceManagement;

namespace Meadow.CLI.Internals.MeadowComms.RecvClasses
{
    // This TCP server directly interacts with Visual Studio debugging.
    // What it receives from Visual Studio it forwards to Meadow.
    // What it receives from Meadow it forwards to Visual Studio.
    public class DebuggingServer
    {
        // VS 2019 - 4024
        // VS 2017 - 4022
        // VS 2015 - 4020
        public IPEndPoint LocalEndpoint { get; private set; }
        ActiveClient activeClient;
        int activeClientCount = 0;

        List<byte[]> buffers = new List<byte[]>();

        // Constructor
        public DebuggingServer(IPEndPoint localEndpoint)
        {
            LocalEndpoint = localEndpoint;
        }

        public async void StartListening(MeadowSerialDevice meadow)
        {
            try
            {
                TcpListener tcpListener = new TcpListener(LocalEndpoint);
                tcpListener.Start();
                LocalEndpoint = (IPEndPoint)tcpListener.LocalEndpoint;
                Console.WriteLine("Listening for Visual Studio to connect");

                while(true)
                {
                    // Wait for client to connect
                    TcpClient tcpClient = await tcpListener.AcceptTcpClientAsync();
                    OnConnect(meadow, tcpClient);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public void Connect(MeadowSerialDevice meadow)
        {
            TcpClient tcpClient = new TcpClient();
            tcpClient.Connect(LocalEndpoint);
            OnConnect(meadow, tcpClient);
        }

        void OnConnect(MeadowSerialDevice meadow, TcpClient tcpClient)
        {
            try
            {
                Console.WriteLine("Visual Studio has connected");
                if (activeClientCount > 0)
                {
                    Debug.Assert(activeClientCount == 1);
                    Debug.Assert(activeClient != null);
                    CloseActiveClient();
                }

                activeClient = new ActiveClient(this, tcpClient);
                lock (buffers)
                {
                    foreach (var buffer in buffers)
                        activeClient.SendToVisualStudio(buffer);
                    buffers.Clear();
                }
                activeClient.ReceiveVSDebug(meadow);
                activeClientCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        internal void CloseActiveClient()
        {
            activeClient.Close();
            activeClient = null;
            activeClientCount = 0;
            lock (buffers)
                buffers.Clear();
        }

        public void SendToVisualStudio(byte[] byteData)
        {
            if (activeClient is ActiveClient ac)
            {
                ac.SendToVisualStudio(byteData);
                return;
            }

            // Buffer the data until VS connects
            lock (buffers)
                buffers.Add(byteData);
        }

        // Imbedded class
        private class ActiveClient
        {
            DebuggingServer debuggingServer;
            TcpClient tcpClient;
            NetworkStream networkStream;
            bool okayToRun;

            // Constructor
            internal ActiveClient(DebuggingServer _debuggingServer, TcpClient _tcpClient)
            {
                debuggingServer = _debuggingServer;
                okayToRun = true;
                tcpClient = _tcpClient;
                networkStream = tcpClient.GetStream();
            }

            internal void Close()
            {
                Console.WriteLine("ActiveClient:Close active client");
                okayToRun = false;
                tcpClient.Close();      // Closes NetworkStream too
            }

            internal async void ReceiveVSDebug(MeadowSerialDevice meadow)
            {
                // Console.WriteLine("ActiveClient:Start receiving from VS");
                try
                {
                    // Receive from Visual Studio and send to Meadow
                    await Task.Run(async () =>
                    {
                        var recvdBuffer = new byte[490];
                        var meadowBuffer = Array.Empty<byte>();
                        while (tcpClient.Connected && okayToRun)
                        {
                            int bytesRead;

                            read:
                            bytesRead = await networkStream.ReadAsync(recvdBuffer, 0, recvdBuffer.Length);
                            if (bytesRead == 0 || !okayToRun)
                                break;

                            var destIndex = meadowBuffer.Length;
                            Array.Resize(ref meadowBuffer, destIndex + bytesRead);
                            Array.Copy(recvdBuffer, 0, meadowBuffer, destIndex, bytesRead);

                            // Ensure we read all the data in this message before passing it along
                            if (networkStream.DataAvailable)
                                goto read;

                            // Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}-Received {bytesRead} bytes from VS will forward to HCOM");

                            // Forward to Meadow
                            MeadowDeviceManager.ForwardVisualStudioDataToMono(meadowBuffer, meadow, 0);
                            //Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}-Forwarded {bytesRead} from VS to Meadow");
                            meadowBuffer = Array.Empty<byte>();
                        }
                    });
                }
                catch (System.IO.IOException ioe)
                {
                    // VS client probably died
                    Console.WriteLine(ioe.ToString());
                    debuggingServer.CloseActiveClient();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    debuggingServer.CloseActiveClient();
                    if (okayToRun)
                        throw;
                }
            }

            public async void SendToVisualStudio(byte[] byteData)
            {
                //Console.WriteLine($"Forwarding {byteData.Length} bytes to VS");
                try
                {
                    // Receive from Meadow and send to Visual Studio
                    if (!tcpClient.Connected)
                    {
                        Console.WriteLine($"Send attempt is not possible, Visual Studio not connected");
                        return;
                    }

                    await networkStream.WriteAsync(byteData, 0, byteData.Length);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                    if (okayToRun)
                        throw;
                }
            }
        }
    }
}