using System.Text;

namespace Meadow.Hcom
{
    public class DeviceInfo
    {
        public Dictionary<string, string> Properties { get; }

        internal DeviceInfo(Dictionary<string, string> properties)
        {
            Properties = properties;
        }

        public string this[string propname] => Properties[propname];
        public string OsVersion => this["OSVersion"];
        public string CoprocessorOsVersion => this["CoprocessorVersion"];
        public string RuntimeVersion => this["MonoVersion"];
        public string Model => this["Model"];
        public string HardwareVersion => this["Hardware"];
        public string DeviceName => this["DeviceName"];
        public string ProcessorType => this["ProcessorType"];
        public string UniqueID => this["ProcessorId"];
        public string SerialNumber => this["SerialNo"];
        public string CoprocessorType => this["CoprocessorType"];
        public string MacAddress => this["WiFiMAC"];

        public override string ToString()
        {
            var info = new StringBuilder();

            foreach (var prop in Properties)
            {
                info.AppendLine($"{prop.Key}: {prop.Value}");
            }

            return info.ToString();
        }
    }
}