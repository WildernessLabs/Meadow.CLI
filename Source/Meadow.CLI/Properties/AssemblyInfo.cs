[assembly: System.Reflection.AssemblyFileVersion(Meadow.CLI.Constants.CLI_VERSION)]
[assembly: System.Reflection.AssemblyVersion(Meadow.CLI.Constants.CLI_VERSION)]
[assembly: System.Reflection.AssemblyInformationalVersion(Meadow.CLI.Constants.CLI_INFORMATIONAL_VERSION)]

namespace Meadow.CLI;

public static class Constants
{
    // AssemblyVersion / AssemblyFileVersion are restricted to numeric four-part versions
    public const string CLI_VERSION = "3.0.0.0";
    // AssemblyInformationalVersion accepts SemVer 2.0 strings including pre-release tags;
    // CliFx reads this for `--version` output
    public const string CLI_INFORMATIONAL_VERSION = "3.0.0-beta3";
}