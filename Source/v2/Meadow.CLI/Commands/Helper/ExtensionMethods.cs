
namespace Meadow.CLI
{
    public static class ExtensionMethods
    {
        public const string ConsoleColourBlack = "\u001b[30m";
        public const string ConsoleColourBlue = "\u001b[34m";
        public const string ConsoleColourCyan = "\u001b[36m";
        public const string ConsoleColourGreen = "\u001b[32m";
        public const string ConsoleColourMagenta = "\u001b[35m";
        public const string ConsoleColourRed = "\u001b[31m";
        public const string ConsoleColourReset = "\u001b[0m";
        public const string ConsoleColourWhite = "\u001b[37m";
        public const string ConsoleColourYellow = "\u001b[33m";

        public static string ColourConsoleText(this string textToColour, string textColour)
        {
            if (!string.IsNullOrEmpty(textToColour))
            {
                return textColour + textToColour + ConsoleColourReset;
            }
            else
            {
                return string.Empty;
            }
        }

        public static string ColourConsoleTextBlack(this string textToColour)
        {
            if (!string.IsNullOrEmpty(textToColour))
            {
                return ConsoleColourBlack + textToColour + ConsoleColourReset;
            }
            else
            {
                return string.Empty;
            }
        }

        public static string ColourConsoleTextCyan(this string textToColour)
        {
            if (!string.IsNullOrEmpty(textToColour))
            {
                return ConsoleColourCyan + textToColour + ConsoleColourReset;
            }
            else
            {
                return string.Empty;
            }
        }

        public static string ColourConsoleTextBlue(this string textToColour)
        {
            if (!string.IsNullOrEmpty(textToColour))
            {
                return ConsoleColourBlue + textToColour + ConsoleColourReset;
            }
            else
            {
                return string.Empty;
            }
        }

        public static string ColourConsoleTextGreen(this string textToColour)
        {
            if (!string.IsNullOrEmpty(textToColour))
            {
                return ConsoleColourGreen + textToColour + ConsoleColourReset;
            }
            else
            {
                return string.Empty;
            }
        }

        public static string ColourConsoleTextMagenta(this string textToColour)
        {
            if (!string.IsNullOrEmpty(textToColour))
            {
                return ConsoleColourMagenta + textToColour + ConsoleColourReset;
            }
            else
            {
                return string.Empty;
            }
        }

        public static string ColourConsoleTextRed(this string textToColour)
        {
            if (!string.IsNullOrEmpty(textToColour))
            {
                return ConsoleColourRed + textToColour + ConsoleColourReset;
            }
            else
            {
                return string.Empty;
            }
        }

        public static string ColourConsoleTextWhite(this string textToColour)
        {
            if (!string.IsNullOrEmpty(textToColour))
            {
                return ConsoleColourWhite + textToColour + ConsoleColourReset;
            }
            else
            {
                return string.Empty;
            }
        }

        public static string ColourConsoleTextYellow(this string textToColour)
        {
            if (!string.IsNullOrEmpty(textToColour))
            {
                return ConsoleColourYellow + textToColour + ConsoleColourReset;
            }
            else
            {
                return string.Empty;
            }
        }
    }
}