using System;
using System.Diagnostics;
using System.IO;
using MeadowCLI.Hcom;

namespace MeadowCLI.DeviceManagement
{
    public static class MeadowFileManager
    {
        static HcomMeadowRequestType meadowRequestType;

        static MeadowFileManager() { }

        public static void WriteFileToFlash(MeadowSerialDevice meadow, string fileName, string targetFileName = null, int partition = 0)
        {
            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_FILE_TRANSFER;

            if (string.IsNullOrWhiteSpace(targetFileName))
            {
                targetFileName = Path.GetFileName(fileName);
            }

            TransmitFileInfoToExtFlash(meadow, meadowRequestType, fileName, targetFileName, partition, false);
        }

        public static void DeleteFile(MeadowSerialDevice meadow, string fileName, int partition = 0)
        {
            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DELETE_FILE_BY_NAME;

            TransmitFileInfoToExtFlash(meadow, meadowRequestType, fileName, fileName, partition, true);
        }

        public static void EraseFlash(MeadowSerialDevice meadow)
        {
            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_BULK_FLASH_ERASE;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(meadowRequestType);
        }

        public static void VerifyErasedFlash(MeadowSerialDevice meadow)
        {
            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_VERIFY_ERASED_FLASH;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(meadowRequestType);
        }

        public static void PartitionFileSystem(MeadowSerialDevice meadow, int numberOfPartitions = 2)
        {
            if (numberOfPartitions < 1 || numberOfPartitions > 8)
            {
                throw new IndexOutOfRangeException("Number of partitions must be between 1 & 8 inclusive");
            }

            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_PARTITION_FLASH_FS;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(meadowRequestType, (uint)numberOfPartitions);
        }

        public static void MountFileSystem(MeadowSerialDevice meadow, int partition)
        {
            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_MOUNT_FLASH_FS;
            new SendTargetData(meadow.SerialPort).SendSimpleCommand(meadowRequestType, (uint)partition);
        }

        public static void InitializeFileSystem(MeadowSerialDevice meadow, int partition)
        {
            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_INITIALIZE_FLASH_FS;
            new SendTargetData(meadow.SerialPort).SendSimpleCommand(meadowRequestType, (uint)partition);
        }

        public static void CreateFileSystem(MeadowSerialDevice meadow)
        {
            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_CREATE_ENTIRE_FLASH_FS;
            new SendTargetData(meadow.SerialPort).SendSimpleCommand(meadowRequestType);
        }

        public static void FormatFileSystem(MeadowSerialDevice meadow, int partition)
        {
            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_FORMAT_FLASH_FILE_SYS;
            new SendTargetData(meadow.SerialPort).SendSimpleCommand(meadowRequestType, (uint)partition);
        }

        public static void ListFiles(MeadowSerialDevice meadow, int partition = 0)
        {
            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_LIST_PARTITION_FILES;
            new SendTargetData(meadow.SerialPort).SendSimpleCommand(meadowRequestType, (uint)partition);
        }

        public static void ListFilesAndCrcs(MeadowSerialDevice meadow, int partition = 0)
        {
            meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_LIST_PART_FILES_AND_CRC;
            new SendTargetData(meadow.SerialPort).SendSimpleCommand(meadowRequestType, (uint)partition);
        }

        private static void TransmitFileInfoToExtFlash(MeadowSerialDevice meadow,
                            HcomMeadowRequestType requestType,
                            string sourceFileName, string targetFileName, int partition,
                            bool deleteFile)
        {
            var sw = new Stopwatch();
            
            var sendTargetData = new SendTargetData(meadow.SerialPort, false);

            try
            {
                //----------------------------------------------
                if (deleteFile == true)
                {
                    // No data packets and no end-of-file message
                    sendTargetData.BuildAndSendFileRelatedCommand(requestType,
                        (UInt32)partition, 0, 0, sourceFileName);
                    return;
                }

                // Open, read and close the data file
                var fileBytes = File.ReadAllBytes(sourceFileName);
                var fileCrc32 = CrcTools.Crc32part(fileBytes, fileBytes.Length, 0);
                var fileLength = fileBytes.Length;

                sw.Start();
                sw.Restart();

                sendTargetData.SendTheEntireFile(targetFileName, (uint)partition,
                    fileBytes, fileCrc32);

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
            HCOM_PROTOCOL_HEADER_TYPE_FILE = 0x0200,
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
            HCOM_PROTOCOL_REQUEST_HEADER_CONTROL_OFFSET = 4,
            HCOM_PROTOCOL_REQUEST_HEADER_RQST_TYPE_OFFSET = 6,
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
            HCOM_MDOW_REQUEST_NO_DIAG_TO_HOST = 0x14 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_SEND_SYSLOG_TO_HOST = 0x15 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,

            // Only used for testing
            HCOM_MDOW_REQUEST_DEVELOPER_1 = 0xf0 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_DEVELOPER_2 = 0xf1 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_DEVELOPER_3 = 0xf2 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_DEVELOPER_4 = 0xf3 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,

            HCOM_MDOW_REQUEST_S25FL_QSPI_INIT  = 0xf4 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_S25FL_QSPI_WRITE = 0xf5 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
            HCOM_MDOW_REQUEST_S25FL_QSPI_READ  = 0xf6 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,

            HCOM_MDOW_REQUEST_START_FILE_TRANSFER = 0x01 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_FILE,
            HCOM_MDOW_REQUEST_DELETE_FILE_BY_NAME = 0x02 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_FILE,

            // Simple debugger message to Meadow
            HCOM_MDOW_REQUEST_DEBUGGER_MSG = 0x01 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_BINARY,
        }

        // Messages sent from meadow to host
        public enum HcomHostRequestType
        {
            HCOM_HOST_REQUEST_UNDEFINED_REQUEST = 0x00 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_UNDEFINED,

            // Simple types
            HCOM_HOST_REQUEST_SIMPLE_MESSAGE = 0x01 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,    // Just the header
            // Simple with some text message
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
            HCOM_HOST_REQUEST_TEXT_MEADOW_DIAG = 0x0B | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_TEXT,
            HCOM_HOST_REQUEST_TEXT_RECONNECT = 0x0C | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_TEXT,
            // Simple with debugger message from Meadow
            HCOM_HOST_REQUEST_DEBUGGER_MSG = 0x01 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_BINARY,
        }
    }
}