﻿using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.Internals.MeadowCommunication;
using Meadow.CLI.Core;

namespace Meadow.CLI.Core.Devices
{
    public class MeadowSocketDevice : MeadowLocalDevice
    {
        private readonly AddressFamily _addressFamily;
        public readonly Socket Socket;

        public MeadowSocketDevice(Socket socket, ILogger<MeadowSocketDevice>? logger = null)
            : base(new MeadowSerialDataProcessor(socket), logger)
        {
            Socket = socket;
        }

        public override bool IsDeviceInitialized()
        {
            throw new NotImplementedException();
        }

        private protected override void Dispose(bool disposing)
        {
            if (disposing)
                Socket?.Dispose();
        }

        public override async Task WriteAsync(byte[] encodedBytes, int encodedToSend, CancellationToken cancellationToken)
        {
            await Task.Yield();
            Socket.Send(encodedBytes, encodedToSend,
                        SocketFlags.None);
        }

        public override Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
            //Socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            //try
            //{
            //    Socket.Connect(endpoint);
            //}
            //catch (SocketException)
            //{
            //    Console.WriteLine("Could not connect to socket, aborting...");
            //    Environment.Exit(1);
            //}
        }

        private static bool TryCreateIPEndPoint(string address,
                                                out IPEndPoint? endpoint)
        {
            if (string.IsNullOrEmpty(address))
            {
                address = string.Empty;
            }
            address = address.Replace("localhost", "127.0.0.1");
            endpoint = null;

            string[] ep = address.Split(':');
            if (ep.Length != 2)
                return false;

            if (!IPAddress.TryParse(ep[0], out IPAddress ip))
                return false;

            int port;
            if (!int.TryParse(ep[1], NumberStyles.None, NumberFormatInfo.CurrentInfo, out port))
                return false;

            endpoint = new IPEndPoint(ip, port);
            return true;
        }
    }
}
