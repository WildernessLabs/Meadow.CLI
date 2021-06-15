using Meadow.CLI.Core.DeviceManagement;

namespace Meadow.CLI.Core.Exceptions
{
    public class DeviceDisconnectedException : MeadowDeviceException
    {
        public DeviceDisconnectedException()
            : base("The Meadow is not longer connected.")
        {
        }
    }
}
