[assembly: System.Reflection.AssemblyFileVersion(Meadow.CLI.Constants.CLI_VERSION)]
[assembly: System.Reflection.AssemblyVersion(Meadow.CLI.Constants.CLI_VERSION)]
[assembly: System.Reflection.AssemblyInformationalVersion(Meadow.CLI.Constants.CLI_VERSION)]

namespace Meadow.CLI
{
    public static class Constants
    {
        public const string CLI_VERSION = "2.0.0.0";

        public const string ConsoleColourRed = "\u001b[31m";
        public const string ConsoleColourGreen = "\u001b[32m";
        public const string ConsoleColourYellow = "\u001b[33m";
        public const string ConsoleColourBlue = "\u001b[34m";
        public const string ConsoleColourClear = "\u001b[0m";

        public static string ColourConsoleText(string? textColour, string? textToColour)
        {
            if (!string.IsNullOrEmpty(textColour)
                && !string.IsNullOrEmpty(textToColour))
            {
                return textColour + textToColour + ConsoleColourClear;
            }
            else
            {
                if (!string.IsNullOrEmpty(textToColour))
                {
                    return textToColour;
                }
                else
                {
                    return string.Empty;
                }
            }
        }
    }
}