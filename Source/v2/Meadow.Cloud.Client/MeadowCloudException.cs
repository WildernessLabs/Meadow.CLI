namespace Meadow.Cloud;

public class MeadowCloudException : Exception
{
    public MeadowCloudException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
