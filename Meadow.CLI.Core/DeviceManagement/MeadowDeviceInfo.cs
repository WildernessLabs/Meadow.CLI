using System;

namespace Meadow.CLI.Core.DeviceManagement
{
    public class MeadowDeviceInfo
    {
        /*
        Meadow by Wilderness Labs,
        Model: F7Micro,
        MeadowOS Version: 0.1.0,
        Processor: STM32F777IIK6,
        Processor Id: 1d-00-29-00-12-51-36-30-33-33-37-33,
        Serial Number: 3360335A3036,
        CoProcessor: ESP32,
        CoProcessor OS Version: 0.1.x\r\n"
        */
        public MeadowDeviceInfo(string deviceInfoString)
        {
            RawDeviceInfo = deviceInfoString;
            Name = deviceInfoString.Substring(0, deviceInfoString.IndexOf(' '));
            Model = ParseValue("Model: ",                          deviceInfoString);
            MeadowOSVersion = ParseValue("MeadowOS Version: ",     deviceInfoString);
            Processor = ParseValue("Processor: ",                  deviceInfoString);
            ProcessorId = ParseValue("Processor Id:",              deviceInfoString);
            SerialNumber = ParseValue("Serial Number: ",           deviceInfoString);
            CoProcessor = ParseValue("CoProcessor: ",              deviceInfoString);
            CoProcessorOs = ParseValue("CoProcessor OS Version: ", deviceInfoString);
        }

        public string RawDeviceInfo { get; }
        public string Name { get; }
        public string Model { get; }
        public string MeadowOSVersion { get; }
        public string Processor { get; }
        public string ProcessorId { get; }
        public string SerialNumber { get; }
        public string CoProcessor { get; }
        public string CoProcessorOs { get; }

        public override string ToString() => RawDeviceInfo;

        private static string ParseValue(string key, string source)
        {
            var start = source.IndexOf(key, StringComparison.Ordinal) + key.Length;
            var end = source.IndexOf(',', start);
            return source.Substring(start, end - start);
        }
    }
}