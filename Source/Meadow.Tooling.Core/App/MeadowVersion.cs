using System;

namespace Meadow.CLI;

public static class MeadowVersion
{
    public static readonly Version V3 = new(3, 0);

    public static bool IsV3OrLater(string osVersion)
    {
        if (Version.TryParse(osVersion, out var version))
        {
            return version >= V3;
        }
        return false;
    }
}
