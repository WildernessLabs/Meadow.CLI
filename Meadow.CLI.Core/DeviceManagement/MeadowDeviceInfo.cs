namespace MeadowCLI.DeviceManagement
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

        public string Name { get; set; } = "Meadow";
        public string Model { get; set; } = "F7Micro";
        public string MeadowOSVersion { get; set; }
        public string Proccessor { get; set; }
        public string ProcessorId { get; set; }
        public string SerialNumber { get; set; }
        public string CoProcessor { get; set; }
        public string CoProcessorOs { get; set; }
    }
}
