using System;
using System.Collections.Concurrent;
using Meadow.CLI.Core.Internals.MeadowCommunication;

namespace Meadow.CLI.Core.DeviceManagement
{
    public abstract class MeadowDataProcessor : IDisposable
    {
        public EventHandler<MeadowMessageEventArgs>? OnReceiveData;
        public BlockingCollection<byte[]> DebuggerMessages = new BlockingCollection<byte[]>();
        public abstract void Dispose();

        public int MaxAllowableMsgPacketLength => 512;
        public int MaxEstimatedSizeOfEncodedPayload => MaxAllowableMsgPacketLength + (MaxAllowableMsgPacketLength / 254) + 8;
        public int ProtocolHeaderSize => 12;
    }

    public class MeadowMessageEventArgs : EventArgs
    {
        public string Message { get; private set; }
        public MeadowMessageType MessageType { get; private set; }

        public MeadowMessageEventArgs(MeadowMessageType messageType, string message = "")
        {
            Message = message;
            MessageType = messageType;
        }
    }
}