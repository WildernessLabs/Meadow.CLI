using System;
using System.Diagnostics;
using System.IO;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Threading.Tasks;
using MeadowCLI.Hcom;

namespace MeadowCLI.DeviceManagement
{
    public static class MeadowFileManager
    {
        static HcomMeadowRequestType meadowRequestType;

        static MeadowFileManager() { }

        public static async Task<bool> WriteFileToFlash(MeadowSerialDevice meadow, string fileName, string targetFileName = null,
            int partition = 0)
        {
            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_FILE_TRANSFER;

            if (string.IsNullOrWhiteSpace(targetFileName))
            {
                targetFileName = Path.GetFileName(fileName);
            }

            // For the STM32F7 on meadow, we need source file and destination file names.
            string[] csvArray = fileName.Split(',');
            if (csvArray.Length == 1)
            {
                // No CSV, just the source file name. So we'll assume the targetFileName is correct
                TransmitFileInfoToExtFlash(meadow, meadowRequestType, fileName, targetFileName, partition, 0, false, true);
                return true;
            }
            else
            {
                // At this point, the fileName field should contain a CSV string containing the source
                // and destionation file names, always in an even number.
                if (csvArray.Length % 2 != 0)
                {
                    Console.WriteLine("Please provide a CSV input with file names \"source, destination, source, destination\"");
                    return false;
                }

                for (int i = 0; i < csvArray.Length; i += 2)
                {
                    // Send files one-by-one
                    bool lastFile = i == csvArray.Length - 2 ? true : false;
                    TransmitFileInfoToExtFlash(meadow, meadowRequestType, csvArray[i].Trim(), csvArray[i + 1].Trim(),
                        partition, 0, false, lastFile);
                }
            }
            return false;
        }

        public static void DeleteFile(MeadowSerialDevice meadow, string fileName, int partition = 0)
        {
            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DELETE_FILE_BY_NAME;

            TransmitFileInfoToExtFlash(meadow, meadowRequestType, fileName, fileName, partition, 0, true);
        }

        public static async Task<bool> EraseFlash(MeadowSerialDevice meadow)
        {
            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_BULK_FLASH_ERASE;
            new SendTargetData(meadow).SendSimpleCommand(meadowRequestType);

            return await MeadowDeviceManager.WaitForResponseMessage(meadow, x => x.Message == "Bulk erase completed");
        }

        

        public static void VerifyErasedFlash(MeadowSerialDevice meadow)
        {
            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_VERIFY_ERASED_FLASH;

            new SendTargetData(meadow).SendSimpleCommand(meadowRequestType);
        }

        public static void PartitionFileSystem(MeadowSerialDevice meadow, int numberOfPartitions = 2)
        {
            if (numberOfPartitions < 1 || numberOfPartitions > 8)
            {
                throw new IndexOutOfRangeException("Number of partitions must be between 1 & 8 inclusive");
            }

            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_PARTITION_FLASH_FS;

            new SendTargetData(meadow).SendSimpleCommand(meadowRequestType, (uint)numberOfPartitions);
        }

        public static void MountFileSystem(MeadowSerialDevice meadow, int partition)
        {
            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_MOUNT_FLASH_FS;
            new SendTargetData(meadow).SendSimpleCommand(meadowRequestType, (uint)partition);
        }

        public static void InitializeFileSystem(MeadowSerialDevice meadow, int partition)
        {
            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_INITIALIZE_FLASH_FS;
            new SendTargetData(meadow).SendSimpleCommand(meadowRequestType, (uint)partition);
        }

        public static void CreateFileSystem(MeadowSerialDevice meadow)
        {
            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_CREATE_ENTIRE_FLASH_FS;
            new SendTargetData(meadow).SendSimpleCommand(meadowRequestType);
        }

        public static void FormatFileSystem(MeadowSerialDevice meadow, int partition)
        {
            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_FORMAT_FLASH_FILE_SYS;
            new SendTargetData(meadow).SendSimpleCommand(meadowRequestType, (uint)partition);
        }

        public static void ListFiles(MeadowSerialDevice meadow, int partition = 0)
        {
            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_LIST_PARTITION_FILES;
            new SendTargetData(meadow).SendSimpleCommand(meadowRequestType, (uint)partition);
        }

        public static void ListFilesAndCrcs(MeadowSerialDevice meadow, int partition = 0)
        {
            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_LIST_PART_FILES_AND_CRC;
            new SendTargetData(meadow).SendSimpleCommand(meadowRequestType, (uint)partition);
        }

        // fileName - is the name of the file on this host PC
        // targetFileName - is the name of the file on the F7
        public static void WriteFileToEspFlash(MeadowSerialDevice meadow, string fileName,
            string targetFileName = null, int partition = 0, string mcuDestAddr = null)
        {
            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER;

            // For the ESP32 on the meadow, we don't need the target file name, we just need the
            // MCU's destination address and the file's binary.
            // Assume if no mcuDestAddr that the fileName is a CSV with both file names and Mcu Addr
            if (mcuDestAddr != null)
            {
                // Since the mcuDestAddr is used we'll assume the fileName field just contains
                // a single file.
                if (string.IsNullOrWhiteSpace(targetFileName))
                {
                    // While not used by the ESP32 it cost nothing to send it and can help
                    // with debugging
                    targetFileName = Path.GetFileName(fileName);
                }

                // Convert mcuDestAddr from a string to a 32-bit unsigned int, but first
                // insure it starts with 0x
                UInt32 mcuAddr = 0;
                if (mcuDestAddr.StartsWith("0x") || mcuDestAddr.StartsWith("0X"))
                {
                    mcuAddr = UInt32.Parse(mcuDestAddr.Substring(2), System.Globalization.NumberStyles.HexNumber);
                }
                else
                {
                    Console.WriteLine($"The '--McuDestAddr' argument must be followed with an address in the form '0x1800'");
                    return;
                }
                TransmitFileInfoToExtFlash(meadow, meadowRequestType, fileName, targetFileName,
                    partition, mcuAddr, false, true);
            }
            else
            {
                // At this point, the fileName field should contain a CSV string containing the destination
                // addresses followed by file's location within the host's file system.
                // E.g. "0x8000, C:\Blink\partition-table.bin, 0x1000, C:\Blink\bootloader.bin, 0x10000, C:\Blink\blink.bin"
                string[] fileElement = fileName.Split(',');
                if (fileElement.Length % 2 != 0)
                {
                    Console.WriteLine("Please provide a CSV input with \"address, fileName, address, fileName\"");
                    return;
                }

                UInt32 mcuAddr;
                for (int i = 0; i < fileElement.Length; i += 2)
                {
                    // Trim any white space from this mcu addr and file name
                    fileElement[i] = fileElement[i].Trim();
                    fileElement[i + 1] = fileElement[i + 1].Trim();

                    if (fileElement[i].StartsWith("0x") || fileElement[i].StartsWith("0X"))
                    {
                        // Fill in the Mcu Addr
                        mcuAddr = UInt32.Parse(fileElement[i].Substring(2), System.Globalization.NumberStyles.HexNumber);
                    }
                    else
                    {
                        Console.WriteLine("Please provide a CSV input with addresses like 0x1234");
                        return;
                    }
                    // Meadow.CLI --Esp32WriteFile --SerialPort Com26 --File
                    // "0x8000, C:\Download\Esp32\Hello\partition-table.bin, 0x1000, C:\Download\Esp32\Hello\bootloader.bin, 0x10000, C:\Download\Esp32\Hello\hello-world.bin"
                    // File Path and Name
                    targetFileName = Path.GetFileName(fileElement[i + 1]);
                    bool lastFile = i == fileElement.Length - 2 ? true : false;
                    TransmitFileInfoToExtFlash(meadow, meadowRequestType, fileElement[i + 1], targetFileName,
                    partition, mcuAddr, false, lastFile);
                }
            }
        }

        private static void TransmitFileInfoToExtFlash(MeadowSerialDevice meadow,
                            HcomMeadowRequestType requestType, string sourceFileName,
                            string targetFileName, int partition, uint mcuAddr,
                            bool deleteFile, bool lastInSeries = false)
        {
            var sw = new Stopwatch();
            
            var sendTargetData = new SendTargetData(meadow, false);

            try
            {
                //----------------------------------------------
                if (deleteFile == true)
                {
                    // No data packets, no end-of-file message and no mcu address
                    sendTargetData.BuildAndSendFileRelatedCommand(requestType,
                        (UInt32)partition, 0, 0, 0, string.Empty, sourceFileName);
                    return;
                }

                // If ESP32 file we must also send the MD5 has of the file
                string md5Hash = string.Empty;
                if (mcuAddr != 0)
                {
                    using (var md5 = MD5.Create())
                    {
                        using (var stream = File.OpenRead(sourceFileName))
                        {
                            var hash = md5.ComputeHash(stream);
                            md5Hash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                        }
                    }
                }

                // Open, read and close the data file
                var fileBytes = File.ReadAllBytes(sourceFileName);
                var fileCrc32 = CrcTools.Crc32part(fileBytes, fileBytes.Length, 0);
                var fileLength = fileBytes.Length;

                sw.Start();
                sw.Restart();

                sendTargetData.SendTheEntireFile(requestType, targetFileName, (uint)partition,
                    fileBytes, mcuAddr, fileCrc32, md5Hash, lastInSeries);

                sw.Stop();

                if (sendTargetData.Verbose) Console.WriteLine($"It took {sw.ElapsedMilliseconds:N0} millisec to send {fileLength:N0} bytes. FileCrc:{fileCrc32:x08}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TransmitFileInfoToExtFlash threw :{ex}");
                throw;
            }
        }

        public enum HcomProtocolHeaderTypes : UInt16
        {
            HCOM_PROTOCOL_HEADER_TYPE_UNDEFINED = 0x0000,
            // Simple request types, include 4-byte user data
            HCOM_PROTOCOL_HEADER_TYPE_SIMPLE = 0x0100,
            // File releted request types, includes 4-byte user data (for the
            // destination partition id), 4-byte file size, 4-byte checksum and
            // variable length destination file name.
            HCOM_PROTOCOL_HEADER_TYPE_FILE_START = 0x0200,
            // Simple text. 
            HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_TEXT = 0x0300,
            // Header followed by binary data. The size of the data can be up to
            // HCOM_PROTOCOL_PACKET_MAX_SIZE minus header size
            HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_BINARY = 0x0400,
        }

        public enum HcomProtocolHeaderOffsets
        {
            HCOM_PROTOCOL_REQUEST_HEADER_SEQ_OFFSET = 0,
            HCOM_PROTOCOL_REQUEST_HEADER_VERSION_OFFSET = 2,
            HCOM_PROTOCOL_REQUEST_HEADER_RQST_TYPE_OFFSET = 4,
            HCOM_PROTOCOL_REQUEST_HEADER_EXTRA_DATA_OFFSET = 6,
            HCOM_PROTOCOL_REQUEST_HEADER_USER_DATA_OFFSET = 8,
        }

        // Messages to be sent to Meadow board from host
        public enum HcomMeadowRequestType : UInt16
        {
            HCOM_MDOW_REQUEST_UNDEFINED_REQUEST = 0x00 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_UNDEFINED,

            HCOM_MDOW_REQUEST_CREATE_ENTIRE_FLASH_FS = 0x01 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_CHANGE_TRACE_LEVEL = 0x02 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_FORMAT_FLASH_FILE_SYS = 0x03 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_END_FILE_TRANSFER = 0x04 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_RESET_PRIMARY_MCU = 0x05 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_VERIFY_ERASED_FLASH = 0x06 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_PARTITION_FLASH_FS = 0x07 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_MOUNT_FLASH_FS = 0x08 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_INITIALIZE_FLASH_FS = 0x09 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_BULK_FLASH_ERASE = 0x0a | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_ENTER_DFU_MODE = 0x0b | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH = 0x0c | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_LIST_PARTITION_FILES = 0x0d | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_LIST_PART_FILES_AND_CRC = 0x0e | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_MONO_DISABLE = 0x0f | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_MONO_ENABLE = 0x10 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_MONO_RUN_STATE = 0x11 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_GET_DEVICE_INFORMATION = 0x12 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_PART_RENEW_FILE_SYS = 0x13 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_NO_TRACE_TO_HOST = 0x14 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_SEND_TRACE_TO_HOST = 0x15 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_END_ESP_FILE_TRANSFER = 0x16 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_READ_ESP_MAC_ADDRESS = 0x17 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_RESTART_ESP32 = 0x18 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_MONO_FLASH = 0x19 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_SEND_TRACE_TO_UART = 0x1a | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_NO_TRACE_TO_UART = 0x1b | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,

            // Only used for testing
            HCOM_MDOW_REQUEST_DEVELOPER_1 = 0xf0 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_DEVELOPER_2 = 0xf1 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_DEVELOPER_3 = 0xf2 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_DEVELOPER_4 = 0xf3 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,

            HCOM_MDOW_REQUEST_S25FL_QSPI_INIT  = 0xf4 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_S25FL_QSPI_WRITE = 0xf5 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_S25FL_QSPI_READ  = 0xf6 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,

            HCOM_MDOW_REQUEST_START_FILE_TRANSFER = 0x01 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_FILE_START,
            HCOM_MDOW_REQUEST_DELETE_FILE_BY_NAME = 0x02 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_FILE_START,
            HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER = 0x03 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_FILE_START,

            // Simple debugger message to Meadow
            HCOM_MDOW_REQUEST_DEBUGGER_MSG = 0x01 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_BINARY,
        }

        // Messages sent from meadow to host
        public enum HcomHostRequestType : UInt16
        {
            HCOM_HOST_REQUEST_UNDEFINED_REQUEST = 0x00 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_UNDEFINED,

            // Simple types
            HCOM_HOST_REQUEST_SIMPLE_MESSAGE = 0x01 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,    // Just the header
            // Simple with some text message
            // Simple with debugger message from Meadow
            HCOM_HOST_REQUEST_MONO_DEBUGGER_MSG = 0x01 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_BINARY,
            HCOM_HOST_REQUEST_TEXT_REJECTED = 0x01 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_TEXT,
            HCOM_HOST_REQUEST_TEXT_ACCEPTED = 0x02 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_TEXT,
            HCOM_HOST_REQUEST_TEXT_CONCLUDED = 0x03 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_TEXT,
            HCOM_HOST_REQUEST_TEXT_ERROR = 0x04 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_TEXT,
            HCOM_HOST_REQUEST_TEXT_INFORMATION = 0x05 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_TEXT,
            HCOM_HOST_REQUEST_TEXT_LIST_HEADER = 0x06 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_TEXT,
            HCOM_HOST_REQUEST_TEXT_LIST_MEMBER = 0x07 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_TEXT,
            HCOM_HOST_REQUEST_TEXT_CRC_MEMBER = 0x08 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_TEXT,
            HCOM_HOST_REQUEST_TEXT_MONO_MSG = 0x09 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_TEXT,
            HCOM_HOST_REQUEST_TEXT_DEVICE_INFO = 0x0A | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_TEXT,
            HCOM_HOST_REQUEST_TEXT_TRACE_MSG = 0x0B | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_TEXT,
            HCOM_HOST_REQUEST_TEXT_RECONNECT = 0x0C | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_TEXT,
        }
    }
}