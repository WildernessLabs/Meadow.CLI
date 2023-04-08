
[assembly: System.Reflection.AssemblyFileVersion(MeadowCLI.Constants.CLI_VERSION)]
[assembly: System.Reflection.AssemblyVersion(MeadowCLI.Constants.CLI_VERSION)]
[assembly: System.Reflection.AssemblyInformationalVersion(MeadowCLI.Constants.CLI_VERSION)]

namespace MeadowCLI
{
    public static class Constants
    {
        public const string CLI_VERSION = "0.97.0";
        public const ushort HCOM_PROTOCOL_CURRENT_VERSION_NUMBER = 0x0007;         // Used for transmission
        public const string WILDERNESS_LABS_USB_VID = "2E6A";
    }
}
