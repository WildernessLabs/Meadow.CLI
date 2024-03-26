namespace Meadow.Hcom;

public class DeviceInfo
{
    public Dictionary<string, string> Properties { get; }

    internal DeviceInfo(Dictionary<string, string> properties)
    {
        Properties = properties;
    }

    public string? this[string propname]
    {
        get
        {
            return Properties.Keys.Contains(propname) ? Properties[propname] : null;
        }
    }

    public string? Product => this["Product"];
    public string? Model => this["Model"];
    public string? ProcessorType => this["ProcessorType"];
    public string? CoprocessorType => this["CoprocessorType"];
    public string? OsVersion => this["OSVersion"];
    public string? CoprocessorOsVersion => this["CoprocessorVersion"];
    public string? ProcessorId => this["ProcessorId"];
    public string? HardwareVersion => this["Hardware"];
    public string? DeviceName => this["DeviceName"];
    public string? RuntimeVersion => this["MonoVersion"];
    public string? SerialNumber => this["SerialNo"];
    public string? MacAddress => this["WiFiMAC"];
    public string? SoftAPMacAddress => this["SoftAPMac"];

    /// <summary>
    /// String representation of an unknown MAC address.
    /// </summary>
    private const string UNKNOWN_MAC_ADDRESS = "00:00:00:00:00:00";

    public override string ToString()
    {
        var deviceInfo = new StringBuilder();

        if (Product != null && Product.Contains(" by Wilderness Labs"))
        {
            deviceInfo.AppendLine(Product);
        }
        else
        {
            deviceInfo.AppendLine($"{Product} by Wilderness Labs");
        }

        deviceInfo.AppendLine("Board Information ");
        deviceInfo.AppendLine($"    Model: {Model}");
        deviceInfo.AppendLine($"    Hardware version: {HardwareVersion}");
        deviceInfo.AppendLine($"    Device name: {DeviceName}");
        deviceInfo.AppendLine();
        deviceInfo.AppendLine($"Hardware Information ");
        deviceInfo.AppendLine($"    Processor type: {ProcessorType}");
        deviceInfo.AppendLine($"    ID: {ProcessorId}");
        deviceInfo.AppendLine($"    Serial number: {SerialNumber}");
        deviceInfo.AppendLine($"    Coprocessor type: {CoprocessorType}");

        string macAddresses = string.Empty;
        int macCount = 0;
        if (!string.IsNullOrEmpty(MacAddress) && MacAddress != UNKNOWN_MAC_ADDRESS)
        {
            macCount++;
            macAddresses += $"\tWiFi: {MacAddress}{Environment.NewLine}";
        }
        if (!string.IsNullOrEmpty(SoftAPMacAddress) && SoftAPMacAddress != UNKNOWN_MAC_ADDRESS)
        {
            macCount++;
            macAddresses += $"\tAP: {SoftAPMacAddress}{Environment.NewLine}";
        }
        if (macCount > 0)
        {
            if (macCount > 1)
            {
                deviceInfo.AppendLine("    MAC Addresses - ");
            }
            else
            {
                deviceInfo.AppendLine("    MAC Address - ");
            }
            deviceInfo.AppendLine($"{macAddresses}");
        }

        deviceInfo.AppendLine();
        deviceInfo.AppendLine($"Firmware Versions ");
        deviceInfo.AppendLine($"    OS: {OsVersion}");
        deviceInfo.AppendLine($"    Runtime: {RuntimeVersion}");
        deviceInfo.AppendLine($"    Coprocessor: {CoprocessorOsVersion}");
        deviceInfo.AppendLine($"    Protocol: {Protocol.HCOM_PROTOCOL_HCOM_VERSION_NUMBER}");

        return deviceInfo.ToString();
    }
}