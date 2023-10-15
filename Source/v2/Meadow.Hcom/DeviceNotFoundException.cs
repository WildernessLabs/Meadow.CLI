namespace Meadow.Hcom
{
    public class DeviceNotFoundException : Exception
    {
        public DeviceNotFoundException(string? message = null, Exception? innerException = null)
            : base(message ?? "No device found on this connection.", innerException)
        {

        }
    }
}