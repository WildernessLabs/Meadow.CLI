using Meadow.CLI.Core.DeviceManagement;
using Meadow.Hcom;
using System;
using System.Diagnostics;
using System.Text;

namespace Meadow.CLI.Core.Internals.MeadowCommunication
{
    public class FileCommand : Command
    {
        internal FileCommand(HcomMeadowRequestType requestType,
                             TimeSpan timeout,
                             string sourceFileName,
                             string destinationFileName,
                             string? md5Hash,
                             uint crc32,
                             int fileSize,
                             uint partition,
                             uint mcuAddress,
                             byte[]? fileBytes,
                             Predicate<MeadowMessageEventArgs> responseHandler,
                             Predicate<MeadowMessageEventArgs> completionHandler,
                             string commandBuilder)
            : base(requestType, timeout, 0, partition, null, responseHandler, completionHandler, null, true, commandBuilder)
        {
            SourceFileName = sourceFileName;
            DestinationFileName = destinationFileName;
            Md5Hash = md5Hash ?? string.Empty;
            Crc32 = crc32;
            FileSize = fileSize;
            Partition = partition;
            McuAddress = mcuAddress;
            FileBytes = fileBytes;
        }

        public string DestinationFileName { get; protected set; }
        public string SourceFileName { get; protected set; }
        public string Md5Hash { get; protected set; }
        public uint Crc32 { get; protected set; }
        public int FileSize { get; protected set; }
        public uint Partition { get; protected set; }
        public uint McuAddress { get; protected set; }
        public byte[]? FileBytes { get; protected set; }

        public override byte[] ToMessageBytes()
        {
            // Allocate the correctly size message buffers
            byte[] targetFileName = Encoding.UTF8.GetBytes(DestinationFileName); // Using UTF-8 works for ASCII but should be Unicode in nuttx

            byte[] md5HashBytes = Encoding.UTF8.GetBytes(Md5Hash);
            int optionalDataLength = sizeof(uint)
                                   + sizeof(uint)
                                   + sizeof(uint)
                                   + HcomProtocolRequestMd5HashLength
                                   + targetFileName.Length;

            byte[] messageBytes = new byte[HcomProtocolCommandRequiredHeaderLength + optionalDataLength];

            var offset = base.ToMessageBytes(ref messageBytes);
            Array.Copy(BitConverter.GetBytes(FileSize), 0, messageBytes, offset, sizeof(uint));
            offset += sizeof(uint);

            // CRC32 checksum or delete file partition number
            Array.Copy(BitConverter.GetBytes(Crc32), 0, messageBytes, offset, sizeof(uint));
            offset += sizeof(uint);

            // MCU address for this file. Used for ESP32 file downloads
            Array.Copy(BitConverter.GetBytes(McuAddress), 0, messageBytes, offset, sizeof(uint));
            offset += sizeof(uint);

            // Include ESP32 MD5 hash if it's needed
            if (string.IsNullOrEmpty(Md5Hash))
                Array.Clear(messageBytes, offset, HcomProtocolRequestMd5HashLength);
            else
                Array.Copy(md5HashBytes, 0, messageBytes, offset, HcomProtocolRequestMd5HashLength);

            offset += HcomProtocolRequestMd5HashLength;

            // Destination File Name
            Array.Copy(targetFileName, 0, messageBytes, offset, targetFileName.Length);
            offset += targetFileName.Length;

            Debug.Assert(offset == optionalDataLength + HcomProtocolCommandRequiredHeaderLength);

            return messageBytes;
        }
    }
}
