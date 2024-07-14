using Meadow.Hcom;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.CLI.Commands.DeviceManagement;

public class MeadowConnectionManager
{
    public const string WILDERNESS_LABS_USB_VID = "2E6A";
    private static readonly object _lockObject = new();

    private readonly ISettingsManager _settingsManager;
    private static IMeadowConnection? _currentConnection;

    private static readonly int RETRY_COUNT = 10;
    private static readonly int RETRY_DELAY = 500;

    public MeadowConnectionManager(ISettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
    }

    public async Task<IMeadowConnection?> GetCurrentConnection(bool forceReconnect = false)
    {
        var route = _settingsManager.GetSetting(SettingsManager.PublicSettings.Route);

        if (route == null)
        {
            throw new Exception($"No 'route' configuration set.{Environment.NewLine}Use the `meadow config route` command. For example:{Environment.NewLine}  > meadow config route COM5");
        }

        return await GetConnectionForRoute(route, forceReconnect);
    }

    public static async Task<IMeadowConnection?> GetConnectionForRoute(string route, bool forceReconnect = false)
    {
        // TODO: support connection changing (CLI does this rarely as it creates a new connection with each command)
        lock (_lockObject)
        {
            if (_currentConnection != null
            && _currentConnection.Name == route
            && forceReconnect == false)
            {
                return _currentConnection;
            }
            else if (_currentConnection != null)
            {
                _currentConnection.Detach();
                _currentConnection.Dispose();
                _currentConnection = null;
            }
        }

        if (route == "local")
        {
            var newConnection = new LocalConnection();
            lock (_lockObject)
            {
                _currentConnection = newConnection;
            }
            return _currentConnection;
        }

        // try to determine what the route is
        string? uri = null;
        if (route.StartsWith("http"))
        {
            uri = route;
        }
        else if (IPAddress.TryParse(route, out var ipAddress))
        {
            uri = $"http://{route}:5000";
        }
        else
        {
            var parts = route.Split(':');
            if (parts.Length == 2 && IPAddress.TryParse(parts[0], out ipAddress) && int.TryParse(parts[1], out var port))
            {
                uri = $"http://{parts[0]}:{port}"; // Construct URI with specified IP and port
            }
        }

        if (uri != null)
        {
            var newConnection = new TcpConnection(uri);
            lock (_lockObject)
            {
                _currentConnection = newConnection;
            }
            return _currentConnection;
        }
        else
        {
            for (int retryCount = 0; retryCount <= RETRY_COUNT; retryCount++)
            {
                try
                {
                    var newConnection = new SerialConnection(route);
                    lock (_lockObject)
                    {
                        _currentConnection = newConnection;
                    }
                    return _currentConnection;
                }
                catch
                {
                    if (retryCount == RETRY_COUNT)
                    {
                        throw new Exception($"Cannot create SerialConnection on route: {route}");
                    }

                    await Task.Delay(RETRY_DELAY);
                }
            }

            // This line should never be reached because the loop will either return or throw
            throw new Exception("Unexpected error in GetCurrentConnection");
        }
    }

    public static async Task<IList<string>> GetSerialPorts()
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

    public static async Task<IList<string>> GetMeadowSerialPortsForOsx()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) == false)
        {
            throw new PlatformNotSupportedException("This method is only supported on macOS");
        }

        return await Task.Run(() =>
        {
            var ports = new List<string>();

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
                    if (startIndex == -1) { continue; }

                    int endIndex = line.IndexOf("\"", startIndex + 1);
                    if (endIndex == -1) { continue; }

                    var port = line.Substring(startIndex, endIndex - startIndex);

                    ports.Add(port);
                    foundMeadow = false;
                }
            }

            return ports;
        });
    }

    public static async Task<IList<string>> GetMeadowSerialPortsForLinux()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) == false)
        {
            throw new PlatformNotSupportedException("This method is only supported on Linux");
        }

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
            var output = proc?.StandardOutput.ReadToEnd();

            if (output == null)
            {
                return Array.Empty<string>();
            }

            return output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)
                  .Where(x => x.Contains("Wilderness_Labs"))
                  .Select(
                      line =>
                      {
                          var parts = line.Split(new[] { "-> " }, StringSplitOptions.RemoveEmptyEntries);
                          var target = parts[1];
                          var port = Path.GetFullPath(Path.Combine(devicePath, target));
                          return port;
                      }).ToArray();
        });
    }

    public static IList<string> GetMeadowSerialPortsForWindows()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) == false)
        {
            throw new PlatformNotSupportedException("This method is only supported on Windows");
        }

        try
        {
            const string WildernessLabsPnpDeviceIDPrefix = @"USB\VID_" + WILDERNESS_LABS_USB_VID;

            // Win32_PnPEntity lives in root\CIMV2
            const string wmiScope = "root\\CIMV2";

            // escape special characters in the device id prefix
            string escapedPrefix = WildernessLabsPnpDeviceIDPrefix.Replace("\\", "\\\\").Replace("_", "[_]");

            // our query for all ports that have a PnP device id starting with Wilderness Labs' USB VID.
            string query = @$"SELECT Name, Caption, PNPDeviceID FROM Win32_PnPEntity WHERE PNPClass = 'Ports' AND PNPDeviceID like '{escapedPrefix}%'";

            List<string> results = new();

            // build the searcher for the query
            using ManagementObjectSearcher searcher = new(wmiScope, query);

            // get the query results
            foreach (ManagementObject moResult in searcher.Get())
            {
                // Try Caption and if not Name, they both seems to contain the COM port 
                string portLongName = $"{moResult["Caption"]}";
                if (string.IsNullOrEmpty(portLongName))
                {
                    portLongName = $"{moResult["Name"]}";
                }
                string pnpDeviceId = $"{moResult["PNPDeviceID"]}";

                // we could collect and return a fair bit of other info from the query:

                //string description = moResult["Description"].ToString();
                //string service = moResult["Service"].ToString();
                //string manufacturer = moResult["Manufacturer"].ToString();

                var comIndex = portLongName.IndexOf("(COM") + 1;
                var copyLength = portLongName.IndexOf(")") - comIndex;
                var port = portLongName.Substring(comIndex, copyLength);

                // the meadow serial is in the device id, after
                // the characters: USB\VID_XXXX&PID_XXXX\
                // so we'll just split is on \ and grab the 3rd element as the format is standard, but the length may vary.
                var splits = pnpDeviceId.Split('\\');
                var serialNumber = splits[2];

                results.Add($"{port}"); // removed serial number for consistency and will break fallback ({serialNumber})");
            }

            return results.ToArray();
        }
        catch (Exception)
        {
            // Since WMI Failed fall back to using SerialPort
            var ports = SerialPort.GetPortNames();

            //hack to skip COM1
            ports = ports.Where((source, index) => source != "COM1").Distinct().ToArray();

            return ports;
        }
    }
}