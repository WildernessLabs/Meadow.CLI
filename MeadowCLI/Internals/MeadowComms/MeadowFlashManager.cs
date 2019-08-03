using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;

namespace MeadowCLI.Hcom
{
    public class MeadowFlashManager
    {
        const UInt16 REQUEST_HEADER_MASK = 0xff00;
        const UInt32 UNKNOWN_USER_DATA = 0xffffffff;

        //ReceiveTargetData _receiveData; //not currently used 

        HcomMeadowRequestType _meadowRequestType;

        public MeadowFlashManager()
        {
        }

        public void WriteFileToFlash(MeadowDevice meadow, string fileName, string targetFileName, int partition)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_FILE_TRANSFER;

        }

        public void DeleteFile(MeadowDevice meadow, string targetFileName, int partition)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DELETE_FILE_BY_NAME;

        }

        public void Erase(MeadowDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_BULK_FLASH_ERASE;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        public void EraseAndVerify(MeadowDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_VERIFY_ERASED_FLASH;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        public void PartitionFileSystem(MeadowDevice meadow, int numberOfPartitions)
        {
            if (numberOfPartitions > 1 || numberOfPartitions > 8)
                throw new IndexOutOfRangeException("Number of partitions must be between 1 & 8 inclusive");

            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_PARTITION_FLASH_FS;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType, (uint)numberOfPartitions);
        }

        public void MountFileSystem(MeadowDevice meadow, int partition)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_MOUNT_FLASH_FS;
            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType, (uint)partition);
        }

        public void InitializeFileSystem(MeadowDevice meadow, int partition)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_INITIALIZE_FLASH_FS;
            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType, (uint)partition);
        }

        public void CreateFileSystem(MeadowDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_CREATE_ENTIRE_FLASH_FS;
            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        public void FormatFileSystem(MeadowDevice meadow, int partition)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_FORMAT_FLASH_FILE_SYS;
            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType, (uint)partition);
        }

        //providing a numeric (0 = none, 1 = info and 2 = debug)
        public void ChangeTraceLevel(MeadowDevice meadow, uint level)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_CHANGE_TRACE_LEVEL;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType, level);
        }

        public void ResetTargetMcu(MeadowDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_RESET_PRIMARY_MCU;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        public void EnterDfuMode(MeadowDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENTER_DFU_MODE;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        public void ToggleNsh(MeadowDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        //ToDo - find the output 
        public void ListFiles(MeadowDevice meadow, int partition)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_LIST_PARTITION_FILES;
            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType, (uint)partition);
        }

        //ToDo - find the output 
        public void ListFilesAndCrcs(MeadowDevice meadow, int partition)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_LIST_PART_FILES_AND_CRC;
            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType, (uint)partition);
        }

        //ToDo - look these up - I assume their developer modes? Should be SetDev1, etc. ?
        public void Developer1(MeadowDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_1;
            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        public void Developer2(MeadowDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_2;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        public void Developer3(MeadowDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_3;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        public void Developer4(MeadowDevice meadow)
        {
            _meadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_4;

            new SendTargetData(meadow.SerialPort).SendSimpleCommand(_meadowRequestType);
        }

        //I don't think this is needed
        //it was used in the original code to determine if the request type alligned to a simple command
        //but simple commands are just commands that only require 0 or 1 numerical args 
        HcomRqstHeaderType GetRequestHeaderType(HcomMeadowRequestType request)
        {
            if (((UInt16)_meadowRequestType & REQUEST_HEADER_MASK) == (UInt16)HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_UNDEFINED)
                return HcomRqstHeaderType.Undefined;

            if (((UInt16)_meadowRequestType & REQUEST_HEADER_MASK) == (UInt16)HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE)
                return HcomRqstHeaderType.Simple;

            if (((UInt16)_meadowRequestType & REQUEST_HEADER_MASK) == (UInt16)HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_FILE)
                return HcomRqstHeaderType.FileType;

            throw new InvalidOperationException(string.Format("Unknown request header type: {0}",
                _meadowRequestType));
        }


        private static void TransmitFileInfoToExtFlash(MeadowDevice meadow,
                            HcomMeadowRequestType requestType,
                            string sourceFileName, string targetFileName, int partition,
                            bool deleteFile)
        {
            var sw = new Stopwatch();

            var sendTargetData = new SendTargetData(meadow.SerialPort);

            try {
                //----------------------------------------------
                if (!deleteFile) {
                    // No data packets and no end-of-file message
                    sendTargetData.BuildAndSendFileRelatedCommand(requestType,
                        (UInt32)partition, 0, 0, targetFileName);
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

                Console.WriteLine("It took {0:N0} millisec to send {1} bytes. FileCrc:{2:x08}",
                    sw.ElapsedMilliseconds, fileLength, fileCrc32);
            } catch (Exception ex) {
                Console.WriteLine($"Unknown exception:{ex}");
            }
        }
    }

    //Enums
    public enum HcomProtocolHeaderTypes : UInt16
    {
        HCOM_PROTOCOL_HEADER_TYPE_UNDEFINED = 0x0000,
        // Simple request types, include 4-byte user data
        HCOM_PROTOCOL_HEADER_TYPE_SIMPLE = 0x0100,
        // File releted request types, includes 4-byte user data (for the
        // destination partition id), 4-byte file size, 4-byte checksum and
        // variable length destition file name.
        HCOM_PROTOCOL_HEADER_TYPE_FILE = 0x0200,
    }

    // Messages to be sent to Meadow board
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

        // Only used for testing
        HCOM_MDOW_REQUEST_DEVELOPER_1 = 0xf0 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
        HCOM_MDOW_REQUEST_DEVELOPER_2 = 0xf1 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
        HCOM_MDOW_REQUEST_DEVELOPER_3 = 0xf2 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
        HCOM_MDOW_REQUEST_DEVELOPER_4 = 0xf3 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,

        HCOM_MDOW_REQUEST_START_FILE_TRANSFER = 0x01 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_FILE,
        HCOM_MDOW_REQUEST_DELETE_FILE_BY_NAME = 0x02 | HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_FILE,
    }

    public enum HcomRqstHeaderType
    {
        Undefined = 0x0000,
        Simple = 0x0000,
        FileType = 0xff0000,
    }
}