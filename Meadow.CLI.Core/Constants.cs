
[assembly: System.Reflection.AssemblyFileVersion(MeadowCLI.Constants.CLI_VERSION)]
[assembly: System.Reflection.AssemblyVersion(MeadowCLI.Constants.CLI_VERSION)]
[assembly: System.Reflection.AssemblyInformationalVersion(MeadowCLI.Constants.CLI_VERSION)]

namespace MeadowCLI
{
    public static class Constants
    {
        public const string CLI_VERSION = "0.14.2";
        public const ushort HCOM_PROTOCOL_CURRENT_VERSION_NUMBER = 0x0006;         // Used for transmission
    }
}
