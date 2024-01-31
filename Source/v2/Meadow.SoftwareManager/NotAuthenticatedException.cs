using System;

namespace Meadow.Software;

public class NotAuthenticatedException : Exception
{
    public NotAuthenticatedException(string message)
        : base(message)
    {
    }
}
