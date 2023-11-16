using System.Collections.Concurrent;
using CliFx.Attributes;
using Meadow.CLI;
using Meadow.CLI.Core.Internals.Dfu;
using Meadow.Cloud;
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

    [CommandOption("file", 'f', IsRequired = false, Description = "Path to OS, Runtime or ESP file")]
    public string? Path { get; set; } = default!;

    [CommandOption("address", 'a', IsRequired = false, Description = "Address location to write the file to")]
    public int? Address { get; set; } = default!;

    private FileManager FileManager { get; }
    private ISettingsManager Settings { get; }

    private const string FileWriteComplete = "Firmware Write Complete!";
    private ILibUsbDevice[]? _libUsbDevices;
    private IMeadowConnection? connection;

    // TODO private bool _fileWriteError = false;

    public FirmwareWriteCommand(ISettingsManager settingsManager, FileManager fileManager, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        FileManager = fileManager;
        Settings = settingsManager;
    }

    protected override async ValueTask ExecuteCommand()
    {
        // do not call the base implementation - it will look for a route, and with DFU we don't have (or need) one

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

        if ((Files != null && Files.Contains(FirmwareType.OS)) && (package != null && package.OsWithoutBootloader == null
            || !deviceSupportsOta
            || UseDfu))
        {
            UseDfu = true;
        }

        if (Files != null && package != null)
        {
            // get the device's serial number via DFU - we'll need it to find the device after it resets
            try
            {
                if (UseDfu)
                {
                    GetLibUsbDevicesInBootloaderModeForCurrentEnvironment();
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex.Message);
                return;
            }

            var flashStatus = new ConcurrentDictionary<string, string>();

            if (_libUsbDevices != null)
            {
                if (_libUsbDevices.Length > 1)
                {
                    await Console!.Output.WriteLineAsync($"Found {_libUsbDevices.Length} devices in bootloader mode.{Environment.NewLine}Would you like to flash them all (Y/N)?");
                    var yesOrNo = await Console!.Input.ReadLineAsync();
                    if (!string.IsNullOrEmpty(yesOrNo))
                    {
                        if (yesOrNo.ToLower() == "n")
                        {
                            Logger?.LogInformation("User elected not to proceed.");
                            return;
                        }
                    }
                }

                foreach (var libUsbDevice in _libUsbDevices)
                {
                    var serialNumber = libUsbDevice.GetDeviceSerialNumber();

                    // no connection is required here - in fact one won't exist
                    // unless maybe we add a "DFUConnection"?

                    try
                    {
                        if (package != null && package.OSWithBootloader != null && Files.Contains(FirmwareType.OS))
                        {
                            flashStatus[serialNumber] = "WritingOS";
                            await WriteOsWithDfu(package.GetFullyQualifiedPath(package.OSWithBootloader), serialNumber);
                        }
                    }
                    catch (Exception ex)
                    {
                        flashStatus[serialNumber] = ex.Message;
                        Logger?.LogError($"Exception type: {ex.GetType().Name}");

                        // TODO: scope this to the right exception type for Win 10 access violation thing
                        // TODO: catch the Win10 DFU error here and change the global provider configuration to "classic"
                        Settings.SaveSetting(SettingsManager.PublicSettings.LibUsb, "classic");

                        Logger?.LogWarning("This machine requires an older version of libusb.  Not to worry, I'll make the change for you, but you will have to re-run this 'firmware write' command.");
                        continue;
                    }

                    var newPort = await MeadowConnectionManager.GetPortFromSerialNumber(serialNumber);

                    if (!string.IsNullOrEmpty(newPort))
                    {
                        Logger?.LogInformation($"Meadow found at {newPort}");

                        // configure the route to that port for the user
                        Settings.SaveSetting(SettingsManager.PublicSettings.Route, newPort);

                        await WriteNonOSToDevice(Files, package, flashStatus);

                        flashStatus[serialNumber] = FileWriteComplete;
                    }

                }

                Logger?.LogInformation($"{Environment.NewLine}Firmware Write Status:");
                foreach (var item in flashStatus)
                {
                    var textColour = ExtensionMethods.ConsoleColourRed;
                    if (item.Value.Contains(FileWriteComplete)) {
                        textColour = ExtensionMethods.ConsoleColourGreen;
                    }
                    Logger?.LogInformation($"Serial Number: {item.Key} - {item.Value}".ColourConsoleText(textColour));
                }
            }
            else
            {
                await WriteNonOSToDevice(Files, package, flashStatus);

                if (connection != null)
                    flashStatus[connection.Name] = FileWriteComplete;
            }
        }
    }

    private async Task WriteNonOSToDevice(FirmwareType[] files, FirmwarePackage? package, ConcurrentDictionary<string, string> flashStatus)
    {
        // get the connection associated with that route
        connection = await GetCurrentConnection();

        try
        {
            if (connection != null && files.Any(f => f != FirmwareType.OS))
            {
                await connection.WaitForMeadowAttach();

                if (CancellationToken.IsCancellationRequested)
                {
                    return;
                }

                flashStatus[connection.Name] = "WriteFiles";
                await WriteFiles(package, connection);
            }
        }
        catch (Exception ex)
        {
            if (connection != null)
                flashStatus[connection.Name] = ex.Message;
            // Log the exception but move onto the next device
            Logger?.LogError($"{Environment.NewLine}Exception type: {ex.GetType().Name}", ex);

            return;
        }
        finally
        {
            // Needed to avoid double messages
            DetachMessageHandlers(connection);
        }
    }

    private void GetLibUsbDevicesInBootloaderModeForCurrentEnvironment()
    {
        // Clear it out each
        if (_libUsbDevices != null && _libUsbDevices.Length > 0)
        {
            _libUsbDevices = Array.Empty<ILibUsbDevice>();
        }

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
                throw new Exception("No devices found in bootloader mode.");
        }

        _libUsbDevices = devices.ToArray();
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

    private async ValueTask WriteFiles(FirmwarePackage? package, IMeadowConnection connection)
    {
        connection.FileWriteFailed += (s, e) =>
        {
            Logger?.LogError($"WriteFiles FAILED!!");
            // TODO _fileWriteError = true;
        };

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
                if (p == 100.0)
                {
                    Console?.Output.WriteAsync($"{Environment.NewLine}");
                }
                else
                {
                    Console?.Output.WriteAsync($"Writing {e.fileName}: {p:0}%     \r");
                }
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
                string? runtime;
                if (!string.IsNullOrEmpty(Path))
                {
                    runtime = Path;
                }
                else
                {
                    // get the path to the runtime bin file
                    runtime = package.Runtime;
                }

                Logger?.LogInformation($"{Environment.NewLine}Writing Runtime {runtime}...");

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
                string? coProcessorFilePath;
                if (!string.IsNullOrEmpty(Path))
                {
                    // use passed in path
                    coProcessorFilePath = Path;
                }
                else
                {
                    // get the default path to the coprocessor bin file
                    coProcessorFilePath = package.CoprocApplication;
                }

                Logger?.LogInformation($"{Environment.NewLine}Writing Coprocessor file {coProcessorFilePath}...");

                string[]? fileList = Array.Empty<string>();
                if (coProcessorFilePath != null
                    && package.CoprocBootloader != null
                    && package.CoprocPartitionTable != null)
                {
                    if (!string.IsNullOrEmpty(Path))
                    {
                        fileList = new string[]
                        {
                            package.GetFullyQualifiedPath(coProcessorFilePath),
                        };
                    }
                    else
                    {
                        fileList = new string[]
                        {
                            package.GetFullyQualifiedPath(coProcessorFilePath),
                            package.GetFullyQualifiedPath(package.CoprocBootloader),
                            package.GetFullyQualifiedPath(package.CoprocPartitionTable),
                        };
                    }
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