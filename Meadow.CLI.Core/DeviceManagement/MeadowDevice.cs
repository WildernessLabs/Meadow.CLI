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
        public const string MSCORLIB = "mscorlib.dll";
        public const string SYSTEM = "System.dll";
        public const string SYSTEM_CORE = "System.Core.dll";
        public const string MEADOW_CORE = "Meadow.dll";
        public const string APP_EXE = "App.exe";

        protected readonly List<string> filesOnDevice = new List<string>();

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

        public async Task<bool> DeployApp(string path)
        {
            await WriteFile(APP_EXE, path);

            //get list of files in folder
            var files = Directory.GetFiles(path, "*.dll");

            //currently deploys all included dlls, update to use CRCs to only deploy new files
            //will likely need to update to deploy other files types (txt, jpg, etc.)
            foreach(var f in files)
            {
                var file = Path.GetFileName(f);
                if (file == MSCORLIB || file == SYSTEM || file == SYSTEM_CORE || file == MEADOW_CORE)
                    continue;

                await WriteFile(file, path);
            }

            return true; //can probably remove bool return type
        }

        public abstract Task<bool> WriteFile(string filename, string path, int timeoutInMs = 200000);

        public abstract Task<List<string>> GetFilesOnDevice(bool refresh = false, int timeoutInMs = 10000);

        public abstract Task<bool> SetDeviceInfo(int timeoutInMs = 500);

        public Task<bool> IsFileOnDevice (string filename)
        {
            return Task.FromResult(filesOnDevice.Contains(filename));
        }

        
    }
}