namespace Meadow.CLI.Core.Exceptions
{
    public class DeviceDisconnectedException : MeadowDeviceException
    {
        public DeviceDisconnectedException()
            : base("The Meadow is no longer connected.")
        {
        }
    }
}
