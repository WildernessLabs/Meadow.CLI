using Meadow.Cli;
using Meadow.Hcom;
using Meadow.LibUsb;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Meadow.CLI.Commands.DeviceManagement;

public class MeadowConnectionManager
{
    public const string WILDERNESS_LABS_USB_VID = "2E6A";
    private static object _lockObject = new();

    private ISettingsManager _settingsManager;
    private IMeadowConnection? _currentConnection;

    public MeadowConnectionManager(ISettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
    }

    public IMeadowConnection? GetCurrentConnection()
    {
        var route = _settingsManager.GetSetting(SettingsManager.PublicSettings.Route);

        if (route == null)
        {
            throw new Exception("No 'route' configuration set");
        }

        // TODO: support connection changing (CLI does this rarely as it creates a new connection with each command)
        if (_currentConnection != null) return _currentConnection;

        // try to determine what the route is
        if (route == "local")
        {
            _currentConnection = new LocalConnection();
        }
        else
        {
            string? uri = null;
            if (route.StartsWith("http"))
            {
                uri = route;
            }
            else if (IPAddress.TryParse(route, out var ipAddress))
            {
                uri = $"http://{route}:5000";
            }
            else if (IPEndPoint.TryParse(route, out var endpoint))
            {
                uri = $"http://{route}";
            }

            if (uri != null)
            {
                _currentConnection = new TcpConnection(uri);
            }
            else
            {
                var retryCount = 0;

            get_serial_connection:
                try
                {
                    _currentConnection = new SerialConnection(route);
                }
                catch
                {
                    retryCount++;
                    if (retryCount > 10)
                    {
                        throw new Exception($"Cannot find port {route}");
                    }
                    Thread.Sleep(500);
                    goto get_serial_connection;
                }
            }
        }

        return _currentConnection;
    }

    public static async Task<IList<MeadowSerialPort>> GetSerialPorts()
    {
        try
        {

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return await GetMeadowSerialPortsForLinux();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return await GetMeadowSerialPortsForOsx();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                lock (_lockObject)
                {
                    return GetMeadowSerialPortsForWindows();
                }
            }
            else
            {
                throw new Exception("Unknown operating system.");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Error Finding Meadow Devices on available Serial Ports: {ex.Message}");
        }
    }

    public static async Task<IList<MeadowSerialPort>> GetMeadowSerialPortsForOsx()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) == false)
            throw new PlatformNotSupportedException("This method is only supported on macOS");

        return await Task.Run(() =>
        {
            var ports = new List<MeadowSerialPort>();

            var psi = new ProcessStartInfo
            {
                FileName = "/usr/sbin/ioreg",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                Arguments = "-r -c IOUSBHostDevice -l"
            };

            string output = string.Empty;

            using (var p = Process.Start(psi))
            {
                if (p != null)
                {
                    output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();
                }
            }

            //split into lines
            var lines = output.Split("\n\r".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);

            var foundMeadow = false;
            foreach (var line in lines)
            {
                if (line.Contains("Meadow F7 Micro"))
                {
                    foundMeadow = true;
                }
                else if (line.IndexOf("+-o", StringComparison.Ordinal) == 0)
                {
                    foundMeadow = false;
                }

                //now find the IODialinDevice entry which contains the serial port name
                if (foundMeadow && line.Contains("IODialinDevice"))
                {
                    int startIndex = line.IndexOf("/");
                    int endIndex = line.IndexOf("\"", startIndex + 1);
                    var port = line.Substring(startIndex, endIndex - startIndex);
                    int serialNumberIndex = line.IndexOf("tty.usbmodem") + 12;
                    var serialNumber = line.Substring(serialNumberIndex, endIndex - serialNumberIndex);

                    ports.Add(new MeadowSerialPort { Name = port, SerialNumber = serialNumber });
                    foundMeadow = false;
                }
            }

            return ports;
        });
    }

    public static async Task<IList<MeadowSerialPort>> GetMeadowSerialPortsForLinux()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) == false)
            throw new PlatformNotSupportedException("This method is only supported on Linux");

        return await Task.Run(() =>
        {
            const string devicePath = "/dev/serial/by-id";
            var psi = new ProcessStartInfo()
            {
                FileName = "ls",
                Arguments = $"-l {devicePath}",
                UseShellExecute = false,
                RedirectStandardOutput = true
            };

            using var proc = Process.Start(psi);
            _ = proc?.WaitForExit(1000);
            var output = proc?.StandardOutput;

            if (output != null)
            {
                var outputText = output.ReadToEnd();
                if (!string.IsNullOrEmpty(outputText))
                {

                    return outputText
                    .Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                          .Where(x => x.Contains("Wilderness_Labs"))
                          .Select(
                              line =>
                              {
                                  var parts = line.Split(new[] { "-> " }, StringSplitOptions.RemoveEmptyEntries);
                                  var target = parts[1];
                                  var port = Path.GetFullPath(Path.Combine(devicePath, target));
                                  int serialNumberIndex = line.IndexOf("ttyACM") + 6;
                                  var serialNumber = line.Substring(serialNumberIndex);

                                  return new MeadowSerialPort { Name = port, SerialNumber = serialNumber };
                              }).ToArray();
                }
            }

            return Array.Empty<MeadowSerialPort>();
        });
    }

    [SupportedOSPlatform("windows")]
    public static IList<MeadowSerialPort> GetMeadowSerialPortsForWindows()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) == false)
            throw new PlatformNotSupportedException("This method is only supported on Windows");

        try
        {
            const string WildernessLabsPnpDeviceIDPrefix = @"USB\VID_" + WILDERNESS_LABS_USB_VID;

            // Win32_PnPEntity lives in root\CIMV2
            const string wmiScope = "root\\CIMV2";

            // escape special characters in the device id prefix
            string escapedPrefix = WildernessLabsPnpDeviceIDPrefix.Replace("\\", "\\\\").Replace("_", "[_]");

            // our query for all ports that have a PnP device id starting with Wilderness Labs' USB VID.
            string query = @$"SELECT Name, Caption, PNPDeviceID FROM Win32_PnPEntity WHERE PNPClass = 'Ports' AND PNPDeviceID like '{escapedPrefix}%'";

            List<MeadowSerialPort> results = new();

            // build the searcher for the query
            using ManagementObjectSearcher searcher = new(wmiScope, query);

            // get the query results
            foreach (ManagementObject moResult in searcher.Get())
            {
                // Try Caption 1st, then Name, they both seem to contain the COM port 
                var captionObject = moResult["Caption"];

                var portLongName = captionObject?.ToString();
                if (string.IsNullOrEmpty(portLongName))
                {
                    var nameObject = moResult["Name"];
                    portLongName = nameObject?.ToString();
                }

                // we could collect and return a fair bit of other info from the query:
                //string description = moResult["Description"].ToString();
                //string service = moResult["Service"].ToString();
                //string manufacturer = moResult["Manufacturer"].ToString();

                if (!string.IsNullOrEmpty(portLongName))
                {
                    var comIndex = portLongName.IndexOf("(COM") + 1;
                    var copyLength = portLongName.IndexOf(")") - comIndex;
                    var port = portLongName.Substring(comIndex, copyLength);

                    var pnpDeviceObject = moResult["PNPDeviceID"];
                    var pnpDeviceId = pnpDeviceObject?.ToString();

                    // the meadow serial is in the device id, after
                    // the characters: USB\VID_XXXX&PID_XXXX\
                    // so we'll just split is on \ and grab the 3rd element as the format is standard, but the length may vary.
                    string? serialNumber = string.Empty;
                    if (!string.IsNullOrEmpty(pnpDeviceId))
                    {
                        var splits = pnpDeviceId.Split('\\');
                        serialNumber = splits[2];
                    }

                    results.Add(new MeadowSerialPort { Name = port, SerialNumber = serialNumber });
                }
            }

            return results.ToArray();
        }
        catch (Exception)
        {
            // Since WMI Failed fall back to using SerialPort
            var ports = SerialPort.GetPortNames();

            //hack to skip COM1
            ports = ports.Where((source, index) => source != "COM1").Distinct().ToArray();

            List<MeadowSerialPort> results = new();
            foreach (var port in ports)
            {
                results.Add(new MeadowSerialPort { Name = port, SerialNumber = string.Empty });
            }

            return results;
        }
    }

    public static async Task<string> GetPortFromSerialNumber(string serialNumber)
    {
        var retryCount = 0;

        string? newPort = null;

        // now wait for the serial port with the passed in serialNumber to appear
        while (newPort == null)
        {
            var ports = await GetSerialPorts();

            if (ports != null)
            {
                foreach (var meadowSerialPort in ports)
                {
                    if (meadowSerialPort.SerialNumber != null && meadowSerialPort.SerialNumber.Contains(serialNumber))
                    {
                        newPort = meadowSerialPort.Name;
                        break;
                    }
                }

                if (retryCount++ > 12)
                {
                    throw new Exception("Meadow device not found");
                }
            }

            await Task.Delay(500);
        }

        return newPort;
    }
}

public class MeadowSerialPort
{
    public string? Name { get; set; }
    public string? SerialNumber { get; set; }
}