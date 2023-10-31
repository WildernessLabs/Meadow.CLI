using System.Text.Json.Serialization;

namespace Meadow.Hcom;

internal class DeviceInfoHttpResponse
{
    /*
    {
        "service": "Wilderness Labs Meadow.Daemon",
        "up_time": 1691423994,
        "version": "1.0",
        "status": "Running",
        "device_info": {
            "serial_number": "d2096851d77a47ad74ff22a862aca5f2",
            "device_name": "DESKTOP-PGERLRJ",
            "platform": "MeadowForLinux",
            "os_version": "#1 SMP Fri Jan 27 02:56:13 UTC 2023",
            "os_release": "5.15.90.1-microsoft-standard-WSL2",
            "os_name": "Linux",
            "machine": "x86_64"
        }
    }
    */
    [JsonPropertyName("service")]
    public string ServiceName { get; set; } = default!;
    [JsonPropertyName("version")]
    public string ServiceVersion { get; set; } = default!;
    [JsonPropertyName("status")]
    public string ServiceStatus { get; set; } = default!;
    [JsonPropertyName("device_info")]
    public DeviceFields DeviceInfo { get; set; } = default!;

    internal class DeviceFields
    {
        [JsonPropertyName("serial_number")]
        public string SerialNumber { get; set; } = default!;
        [JsonPropertyName("device_name")]
        public string DeviceName { get; set; } = default!;
        [JsonPropertyName("platform")]
        public string Platform { get; set; } = default!;
        [JsonPropertyName("os_version")]
        public string OsVersion { get; set; } = default!;
        [JsonPropertyName("os_release")]
        public string OsRelease { get; set; } = default!;
        [JsonPropertyName("os_name")]
        public string OsName { get; set; } = default!;
        [JsonPropertyName("machine")]
        public string Machine { get; set; } = default!;
    }

    public Dictionary<string, string> ToDictionary()
    {
        var d = new Dictionary<string, string>
        {
            { "SerialNumber", DeviceInfo.SerialNumber },
            { "DeviceName", DeviceInfo.DeviceName},
            { "OsVersion", DeviceInfo.OsVersion},
            { "OsName", DeviceInfo.OsName},
        };
        return d;
    }
}
