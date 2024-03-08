
[assembly: System.Reflection.AssemblyFileVersion(Meadow.CLI.Core.Constants.CLI_VERSION)]
[assembly: System.Reflection.AssemblyVersion(Meadow.CLI.Core.Constants.CLI_VERSION)]
[assembly: System.Reflection.AssemblyInformationalVersion(Meadow.CLI.Core.Constants.CLI_VERSION)]

namespace Meadow.CLI.Core
{
    public static class Constants
    {
        public const string CLI_VERSION = "1.9.2.0";
        public const ushort HCOM_PROTOCOL_PREVIOUS_VERSION_NUMBER = 0x0007;
        public const ushort HCOM_PROTOCOL_CURRENT_VERSION_NUMBER = 0x0008;         // Used for transmission
        public const string WILDERNESS_LABS_USB_VID = "2E6A";
        public const string MEADOW_CLOUD_HOST_CONFIG_NAME = "meadowCloudHost";
    }
}
