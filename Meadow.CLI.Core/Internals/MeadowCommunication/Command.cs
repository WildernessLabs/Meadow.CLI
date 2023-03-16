using Meadow.CLI.Core.DeviceManagement;
using Meadow.Hcom;
using MeadowCLI;
using System;

namespace Meadow.CLI.Core.Internals.MeadowCommunication
{
    public class Command
    {
        private protected const int HcomProtocolCommandRequiredHeaderLength = 12;
        private protected const int HcomProtocolCommandSeqNumber = 0;
        // TODO No longer required? private protected const ushort HcomProtocolExtraDataDefaultValue = 0x0000;
        private protected const int HcomProtocolRequestMd5HashLength = 32;

        public Command(HcomMeadowRequestType requestType,
                       TimeSpan timeout,
                       ushort developerLevel,
                       uint userData,
                       Predicate<MeadowMessageEventArgs> responsePredicate,
                       Predicate<MeadowMessageEventArgs> completionPredicate,
                       EventHandler<MeadowMessageEventArgs>? responseHandler,
                       bool isAcknowledged,
                       string commandBuilder)
        {
            RequestType = requestType;
            Timeout = timeout;
            DeveloperLevel = developerLevel;
            UserData = userData;
            ResponsePredicate = responsePredicate;
            CompletionPredicate = completionPredicate;
            ResponseHandler = responseHandler;
            IsAcknowledged = isAcknowledged;
            CommandBuilder = commandBuilder;
        }

        public HcomMeadowRequestType RequestType { get; protected set; }
        public ushort DeveloperLevel { get; protected set; }
        public uint UserData { get; protected set; }
        public TimeSpan Timeout { get; protected set; }
        public byte[]? Data { get; protected set; }
        public Predicate<MeadowMessageEventArgs> ResponsePredicate { get; protected set; }
        public Predicate<MeadowMessageEventArgs> CompletionPredicate { get; protected set; }
        public EventHandler<MeadowMessageEventArgs>? ResponseHandler { get; protected set; }
        public string CommandBuilder { get; protected set; }
        public bool IsAcknowledged { get; set; }

        protected int ToMessageBytes(ref byte[] messageBytes)
        {
            int offset = 0;

            // Two byte seq numb
            Array.Copy(
                BitConverter.GetBytes((ushort)HcomProtocolCommandSeqNumber),
                0,
                messageBytes,
                offset,
                sizeof(ushort));

            offset += sizeof(ushort);

            // Protocol version
            Array.Copy(
                BitConverter.GetBytes(Constants.HCOM_PROTOCOL_CURRENT_VERSION_NUMBER),
                0,
                messageBytes,
                offset,
                sizeof(ushort));

            offset += sizeof(ushort);

            // Command type (2 bytes)
            Array.Copy(
                BitConverter.GetBytes((ushort)RequestType),
                0,
                messageBytes,
                offset,
                sizeof(ushort));

            offset += sizeof(ushort);

            // Extra Data
            Array.Copy(
                BitConverter.GetBytes(DeveloperLevel),
                0,
                messageBytes,
                offset,
                sizeof(ushort));

            offset += sizeof(ushort);

            // User Data
            Array.Copy(BitConverter.GetBytes(UserData), 0, messageBytes, offset, sizeof(uint));
            offset += sizeof(uint);

            if (Data != null)
            {
                Array.Copy(
                    Data,
                    0,
                    messageBytes,
                    HcomProtocolCommandRequiredHeaderLength,
                    Data.Length);

                offset += Data.Length;
            }

            return offset;
        }

        public virtual byte[] ToMessageBytes()
        {
            var messageSize = HcomProtocolCommandRequiredHeaderLength + (Data?.Length).GetValueOrDefault(0);
            var messageBytes = new byte[messageSize];
            // Note: Could use the StructLayout attribute to build
            ToMessageBytes(ref messageBytes);
            return messageBytes;
        }

        public override string ToString()
        {
            return CommandBuilder;
        }
    }
}