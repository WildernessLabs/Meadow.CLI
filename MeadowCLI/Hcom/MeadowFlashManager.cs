using System;
using System.IO.Ports;

namespace MeadowCLI.Hcom
{
    public class MeadowFlashManager
    {
        //replace serial port with a meadow object that knows the serial port
        //it's connected to

        public MeadowFlashManager()
        {
        }

        public bool WriteFileToFlash(Meadow meadow, string fileName, string targetFileName, int partition)
        {
            return false;
        }

        public bool DeleteFile(Meadow meadow, string targetFileName)
        {
            return false;
        }

        public bool Erase(Meadow meadow)
        {
            return false;
        }

        public bool EraseAndVerify(Meadow meadow)
        {
            return false;
        }

        public bool PartitionFileSystem(Meadow meadow)
        {
            return false;
        }

        public bool MountFileSystem(Meadow meadow)
        {
            return false;
        }

        public bool InitializeFileSystem(Meadow meadow)
        {
            return false;
        }

        public bool CreateFileSystem(Meadow meadow)
        {
            return false;
        }

        public bool FormatFileSystem(Meadow meadow)
        {
            return false;
        }

        //providing a numeric (0 = none, 1 = info and 2 = debug)
        public bool ChangeTraceLevel(Meadow meadow, uint level)
        {
            return false;
        }

        public bool ResetTargetMcu(Meadow meadow)
        {
            return false;
        }

        public bool EnterDfuMode(Meadow meadow)
        {
            return false;
        }

        public bool ToggleNsh(Meadow meadow)
        {
            return false;
        }

        public bool ListFiles(Meadow meadow)
        {
            return false;
        }

        public bool ListFileCrcs(Meadow meadow)
        {
            return false;
        }

        //ToDo - look these up - I assume their developer modes? Should be SetDev1, etc. ?
        public bool Developer1(Meadow meadow)
        {
            return false;
        }

        public bool Developer2(Meadow meadow)
        {
            return false;
        }

        public bool Developer3(Meadow meadow)
        {
            return false;
        }

        public bool Developer4(Meadow meadow)
        {
            return false;
        }
    }
}