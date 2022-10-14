using System;

namespace Meadow.CLI.Core
{
    public static class Extensions
    {
        public static Version ToVersion(this string s)
        {
            return Version.TryParse(s, out var result) ? result : new Version();
        }
    }
}