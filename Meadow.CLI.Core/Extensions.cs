using System;
using System.Collections.Generic;
using System.Text;

namespace Meadow.CLI
{
    public static class Extensions
    {
        public static Version ToVersion(this String s)
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
