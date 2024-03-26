using System;

namespace Meadow.CLI.Core
{
    public static class Extensions
    {
        public static Version ToVersion(this string s)
        {
            if (Version.TryParse(s, out var result))
            {
                return result;
            }
            else
            {
                return new Version();
            }
        }
    }
}
