using CliFx.Attributes;
using Meadow.Cli;
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
    public FirmwareType[]? Files { get; set; } = default!;

    private FileManager FileManager { get; }
    private ISettingsManager Settings { get; }

    private ILibUsbDevice? _libUsbDevice;
    // TODO private bool _fileWriteError = false;

    public FirmwareWriteCommand(ISettingsManager settingsManager, FileManager fileManager, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        FileManager = fileManager;
        Settings = settingsManager;
    }

    protected override async ValueTask ExecuteCommand()
    {
        await base.ExecuteCommand();

        if (Connection != null)
        {
            var package = await GetSelectedPackage();

            if (Files == null && package != null)
            {
                Logger?.LogInformation($"Writing all firmware for version '{package.Version}'...");

                Files = new FirmwareType[]
                    {
                    FirmwareType.OS,
                    FirmwareType.Runtime,
                    FirmwareType.ESP
                    };
            }

            if (Files != null && !Files.Contains(FirmwareType.OS) && UseDfu)
            {
                Logger?.LogError($"DFU is only used for OS files.  Select an OS file or remove the DFU option");
                return;
            }

            bool deviceSupportsOta = false; // TODO: get this based on device OS version

            if (package != null && package.OsWithoutBootloader == null
                || !deviceSupportsOta
                || UseDfu)
            {
                UseDfu = true;
            }

            if (UseDfu && Files != null && Files.Contains(FirmwareType.OS) && package != null)
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
                    return;
                }

                var serial = libUsbDevice.GetDeviceSerialNumber();

                // no connection is required here - in fact one won't exist
                // unless maybe we add a "DFUConnection"?

                try
                {
                    if (package != null && package.OSWithBootloader != null)
                    {
                        await WriteOsWithDfu(package.GetFullyQualifiedPath(package.OSWithBootloader), serial);
                    }
                }
                catch (Exception ex)
                {
                    Logger?.LogError($"Exception type: {ex.GetType().Name}");

                    // TODO: scope this to the right exception type for Win 10 access violation thing
                    // TODO: catch the Win10 DFU error here and change the global provider configuration to "classic"
                    Settings.SaveSetting(SettingsManager.PublicSettings.LibUsb, "classic");

                    Logger?.LogWarning("This machine requires an older version of libusb.  Not to worry, I'll make the change for you, but you will have to re-run this 'firmware write' command.");
                    return;
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

                if (Files.Any(f => f != FirmwareType.OS))
                {

                    await Connection.WaitForMeadowAttach();

                    if (CancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    await WriteFiles(Connection);
                }
            }
            else
            {
                await WriteFiles(Connection);
            }

            await Connection.ResetDevice(CancellationToken);
            await Connection.WaitForMeadowAttach();

            if (Connection.Device != null)
            {
                var deviceInfo = await Connection.Device.GetDeviceInfo(CancellationToken);

                if (deviceInfo != null)
                {
                    Logger?.LogInformation(deviceInfo.ToString());
                }
            }
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

            switch (devices.Count)
            {
                case 0:
                    throw new Exception("No device found in bootloader mode");
                case 1:
                    _libUsbDevice = devices[0];
                    break;
                default:
                    throw new Exception("Multiple devices found in bootloader mode.  Disconnect all but one");
            }
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
                Logger?.LogError($"Requested version '{Version}' not found.");
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

    private async ValueTask WriteFiles(IMeadowConnection connection)
    {
        // the connection passes messages back to us (info about actions happening on-device
        connection.DeviceMessageReceived += (s, e) =>
        {
            if (e.message.Contains("% downloaded"))
            {
                // don't echo this, as we're already reporting % written
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
            // TODO _fileWriteError = true;
        };

        var package = await GetSelectedPackage();

        if (Files != null
            && connection.Device != null
            && package != null)
        {
            var wasRuntimeEnabled = await connection.Device.IsRuntimeEnabled(CancellationToken);

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

            if (Files.Contains(FirmwareType.OS))
            {
                if (UseDfu)
                {
                    // this would have already happened before now (in ExecuteAsync) so ignore
                }
                else
                {
                    Logger?.LogInformation($"{Environment.NewLine}Writing OS {package.Version}...");

                    throw new NotSupportedException("OtA writes for the OS are not yet supported");
                }
            }

            if (Files.Contains(FirmwareType.Runtime))
            {
                Logger?.LogInformation($"{Environment.NewLine}Writing Runtime {package.Version}...");

                // get the path to the runtime file
                var runtime = package.Runtime;
                if (string.IsNullOrEmpty(runtime))
                    runtime = string.Empty;
                var rtpath = package.GetFullyQualifiedPath(runtime);

            write_runtime:
                if (!await connection.Device.WriteRuntime(rtpath, CancellationToken))
                {
                    Logger?.LogInformation($"Error writing runtime.  Retrying.");
                    goto write_runtime;
                }
            }

            if (CancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (Files.Contains(FirmwareType.ESP))
            {
                Logger?.LogInformation($"{Environment.NewLine}Writing Coprocessor files...");

                string[]? fileList;
                if (package.CoprocApplication != null
                    && package.CoprocBootloader != null
                    && package.CoprocPartitionTable != null)
                {
                    fileList = new string[]
                        {
                        package.GetFullyQualifiedPath(package.CoprocApplication),
                        package.GetFullyQualifiedPath(package.CoprocBootloader),
                        package.GetFullyQualifiedPath(package.CoprocPartitionTable),
                        };
                }
                else
                {
                    fileList = Array.Empty<string>();
                }

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
    }

    private async Task WriteOsWithDfu(string osFile, string serialNumber)
    {
        await DfuUtils.FlashFile(
            osFile,
            serialNumber,
            logger: Logger,
            format: DfuUtils.DfuFlashFormat.ConsoleOut);
    }
}

