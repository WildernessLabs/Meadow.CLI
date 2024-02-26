namespace Meadow.Cloud.Client.Devices;

public class NotAuthenticatedException : Exception
{
    internal NotAuthenticatedException()
        : base("Client is not authenticated")
    {
    }
}
