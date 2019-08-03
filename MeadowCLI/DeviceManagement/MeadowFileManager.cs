using System;
using MeadowCLI.Hcom;

namespace Meadow.CLI.DeviceManagement
{
    public static class MeadowFileManager
    {
        static MeadowFileManager()
        {
        }

        public static void WriteFileToFlash(MeadowDevice meadow, string fileName, string targetFileName, int partition)
        {
            // TODO: move from FlashManager
        }

        public static void DeleteFile(MeadowDevice meadow, string targetFileName, int partition)
        {
            // TODO: move from FlashManager
        }

        public static void Erase(MeadowDevice meadow)
        {
            // TODO: move from FlashManager
        }

        public static void EraseAndVerify(MeadowDevice meadow)
        {
            // TODO: move from FlashManager
        }

        public static void PartitionFileSystem(MeadowDevice meadow, int numberOfPartitions)
        {
            // TODO: move from FlashManager
        }

        public static void MountFileSystem(MeadowDevice meadow, int partition)
        {
            // TODO: move from FlashManager
        }

        public static void InitializeFileSystem(MeadowDevice meadow, int partition)
        {
            // TODO: move from FlashManager
        }

        public static void CreateFileSystem(MeadowDevice meadow)
        {
            // TODO: move from FlashManager
        }

        public static void FormatFileSystem(MeadowDevice meadow, int partition)
        {
            // TODO: move from FlashManager
        }

        //ToDo - find the output 
        public static void ListFiles(MeadowDevice meadow, int partition)
        {
            // TODO: move from FlashManager
        }

        //ToDo - find the output 
        public static void ListFilesAndCrcs(MeadowDevice meadow, int partition)
        {
            // TODO: move from FlashManager
        }

    }
}
