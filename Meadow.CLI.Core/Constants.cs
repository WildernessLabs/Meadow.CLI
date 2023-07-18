
[assembly: System.Reflection.AssemblyFileVersion(Meadow.CLI.Core.Constants.CLI_VERSION)]
[assembly: System.Reflection.AssemblyVersion(Meadow.CLI.Core.Constants.CLI_VERSION)]
[assembly: System.Reflection.AssemblyInformationalVersion(Meadow.CLI.Core.Constants.CLI_VERSION)]

namespace Meadow.CLI.Core
{
    public static class Constants
    {
        public const string CLI_VERSION = "1.1.1.0";
        public const ushort HCOM_PROTOCOL_PREVIOUS_VERSION_NUMBER = 0x0006;
        public const ushort HCOM_PROTOCOL_CURRENT_VERSION_NUMBER = 0x0007;         // Used for transmission
        public const string WILDERNESS_LABS_USB_VID = "2E6A";
    }
}
