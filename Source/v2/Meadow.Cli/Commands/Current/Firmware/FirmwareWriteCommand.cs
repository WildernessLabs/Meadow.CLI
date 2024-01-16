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

    [CommandOption("use-dfu", 'd', IsRequired = false, Description = "Force using DFU for writing the OS.")]
    public bool UseDfu { get; set; }

    [CommandParameter(0, Name = "Files to write", IsRequired = false)]
    public FirmwareType[]? FirmwareFileTypes { get; set; } = default!;

    private FileManager FileManager { get; }
    private ISettingsManager Settings { get; }

    private ILibUsbDevice? _libUsbDevice;
    private bool _fileWriteError = false;

    public FirmwareWriteCommand(ISettingsManager settingsManager, FileManager fileManager, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        FileManager = fileManager;
        Settings = settingsManager;
    }

    protected override async ValueTask ExecuteCommand()
    {
        var package = await GetSelectedPackage();

        if (package == null)
        {
            Logger?.LogError($"Firware write failed - No package selected");
            return;
        }

        if (FirmwareFileTypes == null)
        {
            Logger?.LogInformation($"Writing all firmware for version '{package.Version}'...");

            FirmwareFileTypes = new FirmwareType[]
            {
                FirmwareType.OS,
                FirmwareType.Runtime,
                FirmwareType.ESP
            };
        }

        bool deviceSupportsOta = false; // TODO: get this based on device OS version

        if (package.OsWithoutBootloader == null
            || !deviceSupportsOta
            || UseDfu)
        {
            UseDfu = true;
        }

        if (!FirmwareFileTypes.Contains(FirmwareType.OS) && UseDfu)
        {
            Logger?.LogError($"DFU is only used for OS files - select an OS file or remove the DFU option");
            return;
        }

        if (UseDfu && FirmwareFileTypes.Contains(FirmwareType.OS))
        {
            var osFile = package.GetFullyQualifiedPath(package.OSWithBootloader);

            if (osFile == null)
            {
                Logger?.LogError($"OS file not found for version '{package.Version}'");
                return;
            }
            if (await WriteOsWithDfu(osFile) == false)
            {
                return;
            }
            //remove from collection to enable writing of other files - ToDo rework this logic
            FirmwareFileTypes = FirmwareFileTypes.Where(t => t != FirmwareType.OS).ToArray();
        }

        IMeadowConnection? connection = null;
        try
        {
            connection = await GetCurrentConnection();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex.Message);
            return;
        }

        if (connection == null || connection.Device == null)
        {
            return;
        }

        await WriteFiles(connection, FirmwareFileTypes);

        await connection.ResetDevice(CancellationToken);
        await connection.WaitForMeadowAttach();

        var deviceInfo = await connection.Device.GetDeviceInfo(CancellationToken);

        if (deviceInfo != null)
        {
            Logger?.LogInformation(deviceInfo.ToString());
        }
    }

    private ILibUsbDevice GetLibUsbDeviceForCurrentEnvironment()
    {
        if (_libUsbDevice == null)
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

            _libUsbDevice = devices.Count switch
            {
                0 => throw new Exception("No device found in bootloader mode"),
                1 => devices[0],
                _ => throw new Exception("Multiple devices found in bootloader mode - only connect one device"),
            };
        }

        return _libUsbDevice;
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

    private async ValueTask WriteFiles(IMeadowConnection connection, FirmwareType[] firmwareFileTypes)
    {
        // the connection passes messages back to us (info about actions happening on-device
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
        connection.FileWriteFailed += (s, e) =>
        {
            _fileWriteError = true;
        };

        var package = await GetSelectedPackage();

        var wasRuntimeEnabled = await connection!.Device!.IsRuntimeEnabled(CancellationToken);

        if (wasRuntimeEnabled)
        {
            Logger?.LogInformation("Disabling device runtime...");
            await connection.Device.RuntimeDisable();
        }

        connection.FileWriteProgress += (s, e) =>
        {
            var p = (e.completed / (double)e.total) * 100d;
            Console?.Output.Write($"Writing {e.fileName}: {p:0}%     \r");
        };

        if (firmwareFileTypes.Contains(FirmwareType.OS))
        {
            Logger?.LogInformation($"{Environment.NewLine}Writing OS {package.Version}...");

            throw new NotSupportedException("OtA writes for the OS are not yet supported");
        }
        if (firmwareFileTypes.Contains(FirmwareType.Runtime))
        {
            Logger?.LogInformation($"{Environment.NewLine}Writing Runtime {package.Version}...");

            // get the path to the runtime file
            var rtpath = package.GetFullyQualifiedPath(package.Runtime);

        write_runtime:
            if (!await connection.Device.WriteRuntime(rtpath, CancellationToken))
            {
                Logger?.LogInformation($"Error writing runtime - retrying");
                goto write_runtime;
            }
        }

        if (CancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (FirmwareFileTypes.Contains(FirmwareType.ESP))
        {
            Logger?.LogInformation($"{Environment.NewLine}Writing Coprocessor files...");

            var fileList = new string[]
                {
                        package.GetFullyQualifiedPath(package.CoprocApplication),
                        package.GetFullyQualifiedPath(package.CoprocBootloader),
                        package.GetFullyQualifiedPath(package.CoprocPartitionTable),
                };

            await connection.Device.WriteCoprocessorFiles(fileList, CancellationToken);

            if (CancellationToken.IsCancellationRequested)
            {
                return;
            }
        }

        Logger?.LogInformation($"{Environment.NewLine}");

        if (wasRuntimeEnabled)
        {
            await connection.Device.RuntimeEnable(CancellationToken);
        }

        // TODO: if we're an F7 device, we need to reset
    }

    private async Task<bool> WriteOsWithDfu(string osFile)
    {
        // get a list of ports - it will not have our meadow in it (since it should be in DFU mode)
        var initialPorts = await MeadowConnectionManager.GetSerialPorts();

        // get the device's serial number via DFU - we'll need it to find the device after it resets
        ILibUsbDevice libUsbDevice;
        try
        {
            libUsbDevice = GetLibUsbDeviceForCurrentEnvironment();
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex.Message);
            return false;
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