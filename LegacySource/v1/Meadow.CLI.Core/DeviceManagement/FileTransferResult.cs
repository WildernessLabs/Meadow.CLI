using System;
using System.Collections.Generic;
using System.Text;

namespace Meadow.CLI.Core.DeviceManagement
{
    public class FileTransferResult
    {
        public FileTransferResult(long transferTime, long fileSize, uint checksum)
        {
            TransferTime = transferTime;
            FileSize = fileSize;
            Checksum = $"{checksum:x08}";
        }
        /// <summary>
        /// Gets the number of milliseconds it took to transfer the file to the device
        /// </summary>
        public long TransferTime {get;}

        /// <summary>
        /// Get the number of bytes written
        /// </summary>
        public long FileSize {get;}

        /// <summary>
        /// Get the CRC Checksum of the file
        /// </summary>
        public string Checksum {get;}

        public static FileTransferResult EmptyResult = new FileTransferResult(0, 0, 0);
    }
}
