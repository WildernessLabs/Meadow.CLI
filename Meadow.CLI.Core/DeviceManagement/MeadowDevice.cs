using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MeadowCLI.Hcom;

namespace MeadowCLI.DeviceManagement
{
    //is this needed?
    public class MeadowDeviceException : Exception
    {
        public MeadowDeviceException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    //a simple model object that represents a meadow device including connection
    public abstract class MeadowDevice
    {
        public MeadowDeviceInfo DeviceInfo { get; protected set; } = new MeadowDeviceInfo();

        //these should move to MeadowDeviceManager 
        [Obsolete]
        public const string MSCORLIB = "mscorlib.dll";
        [Obsolete]
        public const string SYSTEM = "System.dll";
        [Obsolete]
        public const string SYSTEM_CORE = "System.Core.dll";
        [Obsolete]
        public const string MEADOW_CORE = "Meadow.dll";
        [Obsolete]
        public const string APP_EXE = "App.exe";

        public List<string> FilesOnDevice { get; protected set; } = new List<string>();
        public List<UInt32> FileCrcs { get; protected set; } = new List<UInt32>();


        public async Task DeployRequiredLibs(string path, bool forceUpdate = false)
        {
            if(forceUpdate || await IsFileOnDevice(SYSTEM).ConfigureAwait(false) == false)
            {
                await WriteFile(SYSTEM, path).ConfigureAwait(false);
            }

            if (forceUpdate || await IsFileOnDevice(SYSTEM_CORE).ConfigureAwait(false) == false)
            {
                await WriteFile(SYSTEM_CORE, path).ConfigureAwait(false);
            }

            if (forceUpdate || await IsFileOnDevice(MSCORLIB).ConfigureAwait(false) == false)
            {
                await WriteFile(MSCORLIB, path).ConfigureAwait(false);
            }

            if (forceUpdate || await IsFileOnDevice(MEADOW_CORE).ConfigureAwait(false) == false)
            {
                await WriteFile(MEADOW_CORE, path).ConfigureAwait(false);
            }
        }

        public abstract Task<bool> WriteFile(string filename, string path, int timeoutInMs = 200000);

        public abstract Task<List<string>> GetFilesOnDevice(bool refresh = false, int timeoutInMs = 10000);

        public abstract Task<(List<string> files, List<UInt32> crcs)> GetFilesAndCrcs(int timeoutInMs = 10000);

        public abstract Task<bool> SetDeviceInfo(int timeoutInMs = 500);

        public Task<bool> IsFileOnDevice (string filename)
        {
            return Task.FromResult(FilesOnDevice.Contains(filename));
        }

        
    }
}