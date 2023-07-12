using System;

namespace Meadow.CLI.Test
{
    [AttributeUsage(AttributeTargets.Method)]
    public class RequiresDeviceAttribute : Attribute
    {
    }
}