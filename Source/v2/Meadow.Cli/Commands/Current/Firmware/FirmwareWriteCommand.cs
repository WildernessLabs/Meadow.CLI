using CliFx.Attributes;
using Meadow.CLI.Core.Internals.Dfu;
using Meadow.Hcom;
using Meadow.LibUsb;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

public enum FirmwareType
{
    OS,
    Runtime,
    ESP
}

[Command("firmware write", Description = "Writes firmware files to a connected meadow device")]
public class FirmwareWriteCommand : BaseDeviceCommand<FirmwareWriteCommand>
{
    [CommandOption("version", 'v', IsRequired = false)]
    public string? Version { get; set; }

    [CommandOption("use-dfu", 'd', IsRequired = false, Description = "Force using DFU/HCOM for writing files.")]
    public bool UseDfu { get; set; }

    [CommandOption("file", 'f', IsRequired = false, Description = "Send only the specified file")]
    public string? IndividualFile { get; set; }

    [CommandParameter(0, Name = "Files to write", IsRequired = false)]
    public FirmwareType[]? FirmwareFileTypes { get; set; } = default!;

    private FileManager FileManager { get; }
    private ISettingsManager Settings { get; }

    public FirmwareWriteCommand(ISettingsManager settingsManager, FileManager fileManager, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        FileManager = fileManager;
        Settings = settingsManager;
    }

    private int _lastWriteProgress = 0;

    private async Task<IMeadowConnection?> GetConnectionAndDisableRuntime()
    {
        var connection = await GetCurrentConnection();

        if (connection == null || connection.Device == null)
        {
            // can't find a device
            return null;
        }

        connection.FileWriteProgress += (s, e) =>
        {
            var p = (int)((e.completed / (double)e.total) * 100d);
            // don't report < 10% increments (decrease spew on large files)
            if (p - _lastWriteProgress < 10) { return; }

            _lastWriteProgress = p;

            Logger?.LogInformation($"Writing {e.fileName}: {p:0}%     {(p < 100 ? string.Empty : "\r\n")}");
        };
        connection.DeviceMessageReceived += (s, e) =>
        {
            if (e.message.Contains("% downloaded"))
            {   // don't echo this, as we're already reporting % written
            }
            else
            {
                Logger?.LogInformation(e.message);
            }
        };
        connection.ConnectionMessage += (s, message) =>
        {
            Logger?.LogInformation(message);
        };

        if (await connection.Device.IsRuntimeEnabled())
        {
            Logger?.LogInformation("Disabling device runtime...");
            await connection.Device.RuntimeDisable();
        }

        return connection;
    }

    private bool RequiresDfuForRuntimeUpdates(DeviceInfo info)
    {
        if (System.Version.TryParse(info.OsVersion, out var version))
        {
            return version.Major switch
            {
                0 => true,
                1 => version.Minor < 8,
                _ => false,
            };
        }

        return true;
    }

    private bool RequiresDfuForEspUpdates(DeviceInfo info)
    {
        if (System.Version.TryParse(info.OsVersion, out var version))
        {
            return version.Major switch
            {
                0 => true,
                1 => version.Minor < 9,
                _ => false,
            };
        }

        return true;
    }

    protected override async ValueTask ExecuteCommand()
    {
        var package = await GetSelectedPackage();

        if (package == null)
        {
            Logger?.LogError($"Firmware write failed - No package selected");
            return;
        }

        if (IndividualFile != null)
        {
            // check the file exists
            var fullPath = Path.GetFullPath(IndividualFile);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException(fullPath);
            }

            // set the file type
            FirmwareFileTypes = Path.GetFileName(IndividualFile) switch
            {
                F7FirmwarePackageCollection.F7FirmwareFiles.OSWithBootloaderFile => new[] { FirmwareType.OS },
                F7FirmwarePackageCollection.F7FirmwareFiles.OsWithoutBootloaderFile => new[] { FirmwareType.OS },
                F7FirmwarePackageCollection.F7FirmwareFiles.RuntimeFile => new[] { FirmwareType.Runtime },
                F7FirmwarePackageCollection.F7FirmwareFiles.CoprocApplicationFile => new[] { FirmwareType.ESP },
                F7FirmwarePackageCollection.F7FirmwareFiles.CoprocBootloaderFile => new[] { FirmwareType.ESP },
                F7FirmwarePackageCollection.F7FirmwareFiles.CoprocPartitionTableFile => new[] { FirmwareType.ESP },
                _ => throw new ArgumentException($"Unknown firmware file {Path.GetFileName(IndividualFile)}")
            };

            Logger?.LogInformation($"Writing firmware file '{fullPath}'...");
        }
        else if (FirmwareFileTypes == null)
        {
            Logger?.LogInformation($"Writing all firmware for version '{package.Version}'...");

            FirmwareFileTypes = new FirmwareType[]
            {
                FirmwareType.OS,
                FirmwareType.Runtime,
                FirmwareType.ESP
            };
        }

        IMeadowConnection? connection = null;
        DeviceInfo? deviceInfo = null;

        if (FirmwareFileTypes.Contains(FirmwareType.OS))
        {
            var osFileWithBootloader = package.GetFullyQualifiedPath(package.OSWithBootloader);
            var osFileWithoutBootloader = package.GetFullyQualifiedPath(package.OsWithoutBootloader);

            if (osFileWithBootloader == null && osFileWithoutBootloader == null)
            {
                Logger?.LogError($"OS file not found for version '{package.Version}'");
                return;
            }

            // do we have a dfu device attached, or is DFU specified?
            var dfuDevice = GetLibUsbDeviceForCurrentEnvironment();

            if (dfuDevice != null)
            {
                Logger?.LogInformation($"DFU device detected - using DFU to write OS");
                UseDfu = true;
            }
            else
            {
                connection = await GetConnectionAndDisableRuntime();

                if (connection == null)
                {
                    // couldn't find a connected device
                    Logger?.LogError($"Unable to detect a connected device");
                    return;
                }
                deviceInfo = await connection.GetDeviceInfo(CancellationToken);
            }

            if (UseDfu || dfuDevice != null || osFileWithoutBootloader == null || RequiresDfuForRuntimeUpdates(deviceInfo!))
            {
                if (await WriteOsWithDfu(dfuDevice, osFileWithBootloader) == false)
                {
                    return;
                }

                dfuDevice?.Dispose();

                await Task.Delay(1500);

                connection = await GetConnectionAndDisableRuntime();

                if (connection == null)
                {
                    // couldn't find a connected device
                    Logger?.LogError($"Unable to detect a connected device");
                    return;
                }
                await connection.WaitForMeadowAttach();
            }
            else
            {
                await connection!.Device!.WriteFile(osFileWithoutBootloader, $"/meadow0/update/os/{package.OsWithoutBootloader}");
            }
        }

        if (FirmwareFileTypes.Contains(FirmwareType.Runtime) || Path.GetFileName(IndividualFile) == F7FirmwarePackageCollection.F7FirmwareFiles.RuntimeFile)
        {
            connection = await WriteFirmware(connection, deviceInfo, package);

            if (connection == null)
            {
                // couldn't find a connected device
                Logger?.LogError($"Unable to detect a connected device");
                return;
            }

            await connection.WaitForMeadowAttach();
        }

        if (FirmwareFileTypes.Contains(FirmwareType.ESP)
             || Path.GetFileName(IndividualFile) == F7FirmwarePackageCollection.F7FirmwareFiles.CoprocPartitionTableFile
             || Path.GetFileName(IndividualFile) == F7FirmwarePackageCollection.F7FirmwareFiles.CoprocApplicationFile
             || Path.GetFileName(IndividualFile) == F7FirmwarePackageCollection.F7FirmwareFiles.CoprocBootloaderFile)
        {
            await WriteEspFiles(connection, deviceInfo, package);
        }

        // reset device
        if (connection != null && connection.Device != null)
        {
            await connection.Device.Reset();
        }
    }

    private async Task<IMeadowConnection?> WriteFirmware(IMeadowConnection? connection, DeviceInfo? deviceInfo, FirmwarePackage package)
    {
        if (connection == null)
        {
            connection = await GetConnectionAndDisableRuntime();

            if (connection == null) { return null; } // couldn't find a connected device
        }

        Logger?.LogInformation($"{Environment.NewLine}Writing Runtime {package.Version}...");

        // get the path to the runtime file
        var rtpath = package.GetFullyQualifiedPath(package.Runtime);

        if (deviceInfo == null)
        {
            deviceInfo = await connection.GetDeviceInfo(CancellationToken);
        }

        if (UseDfu || RequiresDfuForRuntimeUpdates(deviceInfo))
        {

        write_runtime:
            if (!await connection.Device!.WriteRuntime(rtpath, CancellationToken))
            {
                // TODO: implement a retry timeout
                Logger?.LogInformation($"Error writing runtime - retrying");
                goto write_runtime;
            }
        }
        else
        {
            await connection.Device!.WriteFile(rtpath, $"/meadow0/update/os/{package.Runtime}");
        }

        return connection;
    }

    private async Task WriteEspFiles(IMeadowConnection? connection, DeviceInfo? deviceInfo, FirmwarePackage package)
    {
        if (connection == null)
        {
            connection = await GetConnectionAndDisableRuntime();
            if (connection == null) return; // couldn't find a connected device
        }

        Logger?.LogInformation($"{Environment.NewLine}Writing Coprocessor files...");

        string[] fileList;

        if (IndividualFile != null)
        {
            fileList = new string[] { IndividualFile };
        }
        else
        {
            fileList = new string[]
            {
                package.GetFullyQualifiedPath(package.CoprocApplication),
                package.GetFullyQualifiedPath(package.CoprocBootloader),
                package.GetFullyQualifiedPath(package.CoprocPartitionTable),
            };
        }

        if (deviceInfo == null)
        {
            deviceInfo = await connection.GetDeviceInfo(CancellationToken);
        }

        if (UseDfu || RequiresDfuForEspUpdates(deviceInfo))
        {
            await connection.Device!.WriteCoprocessorFiles(fileList, CancellationToken);
        }
        else
        {
            foreach (var file in fileList)
            {
                await connection!.Device!.WriteFile(file, $"/meadow0/update/os/{Path.GetFileName(file)}");
            }
        }
    }

    private ILibUsbDevice? GetLibUsbDeviceForCurrentEnvironment()
    {
        ILibUsbProvider provider;

        // TODO: read the settings manager to decide which provider to use (default to non-classic)
        var setting = Settings.GetAppSetting(SettingsManager.PublicSettings.LibUsb);
        if (setting == "classic")
        {
            provider = new ClassicLibUsbProvider();
        }
        else
        {
            provider = new LibUsbProvider();
        }

        var devices = provider.GetDevicesInBootloaderMode();

        return devices.Count switch
        {
            0 => null,
            1 => devices[0],
            _ => throw new Exception("Multiple devices found in bootloader mode - only connect one device"),
        };
    }

    private async Task<FirmwarePackage?> GetSelectedPackage()
    {
        await FileManager.Refresh();

        var collection = FileManager.Firmware["Meadow F7"];
        FirmwarePackage package;

        if (Version != null)
        {
            // make sure the requested version exists
            var existing = collection.FirstOrDefault(v => v.Version == Version);

            if (existing == null)
            {
                Logger?.LogError($"Requested version '{Version}' not found");
                return null;
            }
            package = existing;
        }
        else
        {
            Version = collection.DefaultPackage?.Version ??
                throw new Exception("No default version set");

            package = collection.DefaultPackage;
        }

        return package;
    }

    private async Task<bool> WriteOsWithDfu(ILibUsbDevice? libUsbDevice, string osFile)
    {
        // get a list of ports - it will not have our meadow in it (since it should be in DFU mode)
        var initialPorts = await MeadowConnectionManager.GetSerialPorts();

        // get the device's serial number via DFU - we'll need it to find the device after it resets
        if (libUsbDevice == null)
        {
            try
            {
                libUsbDevice = GetLibUsbDeviceForCurrentEnvironment();
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex.Message);
                return false;
            }
        }

        string serialNumber;
        try
        {
            serialNumber = libUsbDevice.GetDeviceSerialNumber();
        }
        catch
        {
            Logger?.LogError("Firmware write failed - unable to read device serial number (make sure device is connected)");
            return false;
        }

        try
        {
            await DfuUtils.FlashFile(
            osFile,
            serialNumber,
            logger: Logger,
            format: DfuUtils.DfuFlashFormat.ConsoleOut);
        }
        catch (ArgumentException ex)
        {
            Logger?.LogWarning("Unable to write firmware with Dfu - is Dfu-util installed? Run `meadow dfu install` to install");
            return false;
        }
        catch (Exception ex)
        {
            Logger?.LogError($"Exception type: {ex.GetType().Name}");

            // TODO: scope this to the right exception type for Win 10 access violation thing
            // TODO: catch the Win10 DFU error here and change the global provider configuration to "classic"
            Settings.SaveSetting(SettingsManager.PublicSettings.LibUsb, "classic");

            Logger?.LogWarning("This machine requires an older version of LibUsb. The CLI settings have been updated, re-run the 'firmware write' command to update your device.");
            return false;
        }

        // now wait for a new serial port to appear
        var ports = await MeadowConnectionManager.GetSerialPorts();
        var retryCount = 0;

        var newPort = ports.Except(initialPorts).FirstOrDefault();

        while (newPort == null)
        {
            if (retryCount++ > 10)
            {
                throw new Exception("New meadow device not found");
            }
            await Task.Delay(500);
            ports = await MeadowConnectionManager.GetSerialPorts();
            newPort = ports.Except(initialPorts).FirstOrDefault();
        }

        Logger?.LogInformation($"Meadow found at {newPort}");

        // configure the route to that port for the user
        Settings.SaveSetting(SettingsManager.PublicSettings.Route, newPort);

        return true;
    }
}