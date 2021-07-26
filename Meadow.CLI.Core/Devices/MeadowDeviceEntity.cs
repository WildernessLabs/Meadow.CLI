namespace Meadow.CLI.Core.Devices
{
    public class MeadowDeviceEntity
    {
        public MeadowDeviceEntity(string port, string? serialNumber)
        {
            Port = port;
            SerialNumber = serialNumber;
        }

        public string Port { get;set;}
        public string? SerialNumber { get;set;}

        public override string ToString()
        {
            return $"Port: {Port}, SerialNumber: {SerialNumber}";
        }
    }
}
