using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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

        public List<string> FilesOnDevice { get; protected set; } = new List<string>();
        public List<UInt32> FileCrcs { get; protected set; } = new List<UInt32>();
        
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