using System;

namespace Meadow.CLI.Core.Devices
{
    public class MeadowDeviceEntity
    {
        public MeadowDeviceEntity(string port, string? serialNumber)
        {
            if (string.IsNullOrWhiteSpace(port))
            {
                throw new ArgumentNullException(nameof(port));
            }
            if (string.IsNullOrWhiteSpace(serialNumber))
            {
                throw new ArgumentNullException(nameof(serialNumber));
            }
            Port = port;
            SerialNumber = serialNumber;
        }

        public string Port { get; set; }
        public string? SerialNumber { get; set; }

        public override string ToString()
        {
            return $"Port: {Port}, SerialNumber: {SerialNumber}";
        }
    }
}