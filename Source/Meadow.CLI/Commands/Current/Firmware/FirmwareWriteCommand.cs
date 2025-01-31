using CliFx.Attributes;
using Meadow.CLI.Core.Internals.Dfu;
using Meadow.Hcom;
using Meadow.LibUsb;
using Meadow.Software;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

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

    [CommandOption("use-dfu", 'd', IsRequired = false, Description = "Force using DFU/HCOM for writing files")]
    public bool UseDfu { get; set; } = true;

    [CommandOption("file", 'f', IsRequired = false, Description = "Send only the specified file")]
    public string? IndividualFile { get; set; }

    [CommandParameter(0, Description = "Files to write", IsRequired = false)]
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

    private async Task<IMeadowConnection> GetConnectionAndDisableRuntime(string? route = null)
    {
        IMeadowConnection connection;

        if (route != null)
        {
            connection = await GetConnectionForRoute(route, true);
        }
        else
        {
            connection = await GetCurrentConnection(true);
        }

        if (await connection.Device!.IsRuntimeEnabled())
        {
            Logger?.LogInformation($"{Strings.DisablingRuntime}...");
            await connection.Device.RuntimeDisable();
        }

        _lastWriteProgress = 0;

        connection.FileWriteProgress += (s, e) =>
        {
            var p = (int)(e.completed / (double)e.total * 100d);
            // don't report < 10% increments (decrease spew on large files)
            if (p - _lastWriteProgress < 10) { return; }

            _lastWriteProgress = p;

            Logger?.LogInformation($"{Strings.Writing} {e.fileName}: {p:0}%     {(p < 100 ? string.Empty : "\r\n")}");
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

        return connection;
    }

    private bool RequiresDfuForRuntimeUpdates(DeviceInfo info)
    {
        return true;
        /*
        restore this when we support OtA-style updates again
        if (System.Version.TryParse(info.OsVersion, out var version))
        {
            return version.Major >= 2;
        }
        */
    }

    private bool RequiresDfuForEspUpdates(DeviceInfo info)
    {
        return true;
    }

    protected override async ValueTask ExecuteCommand()
    {
        var package = await GetSelectedPackage();

        if (package == null)
        {
            return;
        }

        if (IndividualFile != null)
        {
            // check the file exists
            var fullPath = Path.GetFullPath(IndividualFile);
            if (!File.Exists(fullPath))
            {
                throw new CommandException(string.Format(Strings.InvalidFirmwareForSpecifiedPath, fullPath), CommandExitCode.FileNotFound);
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
                _ => throw new CommandException(string.Format(Strings.UnknownSpecifiedFirmwareFile, Path.GetFileName(IndividualFile)))
            };

            Logger?.LogInformation(string.Format($"{Strings.WritingSpecifiedFirmwareFile}...", fullPath));
        }
        else if (FirmwareFileTypes == null)
        {
            Logger?.LogInformation(string.Format(Strings.WritingAllFirmwareForSpecifiedVersion, package.Version));

            FirmwareFileTypes = new FirmwareType[]
            {
                FirmwareType.OS,
                FirmwareType.Runtime,
                FirmwareType.ESP
            };
        }
        else if (FirmwareFileTypes.Length == 1 && FirmwareFileTypes[0] == FirmwareType.Runtime)
        {   //use the "DFU" path when only writing the runtime
            UseDfu = true;
        }

        IMeadowConnection? connection = null;
        DeviceInfo? deviceInfo = null;

        if (FirmwareFileTypes.Contains(FirmwareType.OS))
        {
            string? osFileWithBootloader = null;
            string? osFileWithoutBootloader = null;

            if (string.IsNullOrWhiteSpace(IndividualFile))
            {
                osFileWithBootloader = package.GetFullyQualifiedPath(package.OSWithBootloader);
                osFileWithoutBootloader = package.GetFullyQualifiedPath(package.OsWithoutBootloader);

                if (osFileWithBootloader == null && osFileWithoutBootloader == null)
                {
                    throw new CommandException(string.Format(Strings.OsFileNotFoundForSpecifiedVersion, package.Version));
                }
            }
            else
            {
                osFileWithBootloader = IndividualFile;
            }

            // do we have a dfu device attached, or is DFU specified?
            var provider = new LibUsbProvider();
            var dfuDevice = GetLibUsbDeviceForCurrentEnvironment(provider);
            bool ignoreSerial = IgnoreSerialNumberForDfu(provider);

            if (dfuDevice != null)
            {
                Logger?.LogInformation($"{Strings.DfuDeviceDetected} - {Strings.UsingDfuToWriteOs}");
                UseDfu = true;
            }
            else
            {
                if (UseDfu)
                {
                    throw new CommandException(Strings.NoDfuDeviceDetected);
                }

                connection = await GetConnectionAndDisableRuntime();

                deviceInfo = await connection.GetDeviceInfo(CancellationToken);
            }

            if (UseDfu || dfuDevice != null || osFileWithoutBootloader == null || RequiresDfuForRuntimeUpdates(deviceInfo!))
            {
                // get a list of ports - it will not have our meadow in it (since it should be in DFU mode)
                var initialPorts = await MeadowConnectionManager.GetSerialPorts();

                try
                {
                    await WriteOsWithDfu(dfuDevice!, osFileWithBootloader!, ignoreSerial);
                }
                finally
                {
                    dfuDevice?.Dispose();
                }

                await Task.Delay(1500);

                connection = await FindMeadowConnection(initialPorts);

                await connection.WaitForMeadowAttach();
            }
            else
            {
                await connection!.Device!.WriteFile(osFileWithoutBootloader, $"/{AppManager.MeadowRootFolder}/update/os/{package.OsWithoutBootloader}");
            }
        }

        if (FirmwareFileTypes.Contains(FirmwareType.Runtime) || Path.GetFileName(IndividualFile) == F7FirmwarePackageCollection.F7FirmwareFiles.RuntimeFile)
        {
            if (string.IsNullOrEmpty(IndividualFile))
            {
                connection = await WriteRuntime(connection, deviceInfo, package);
            }
            else
            {
                connection = await WriteRuntime(connection, deviceInfo, IndividualFile, Path.GetFileName(IndividualFile));
            }

            if (connection == null)
            {
                throw CommandException.MeadowDeviceNotFound;
            }

            await connection.WaitForMeadowAttach();
        }

        if (FirmwareFileTypes.Contains(FirmwareType.ESP)
             || Path.GetFileName(IndividualFile) == F7FirmwarePackageCollection.F7FirmwareFiles.CoprocPartitionTableFile
             || Path.GetFileName(IndividualFile) == F7FirmwarePackageCollection.F7FirmwareFiles.CoprocApplicationFile
             || Path.GetFileName(IndividualFile) == F7FirmwarePackageCollection.F7FirmwareFiles.CoprocBootloaderFile)
        {
            connection = await GetConnectionAndDisableRuntime();

            await WriteEspFiles(connection, deviceInfo, package);

            await connection.WaitForMeadowAttach();

            // reset device
            if (connection != null && connection.Device != null)
            {
                await connection.Device.Reset();
            }
        }

        Logger?.LogInformation(Strings.FirmwareUpdatedSuccessfully);
    }

    private async Task<IMeadowConnection> FindMeadowConnection(IList<string> portsToIgnore)
    {
        IMeadowConnection? connection = null;

        var newPorts = await WaitForNewSerialPorts(portsToIgnore);
        string newPort = string.Empty;

        if (newPorts == null)
        {
            throw CommandException.MeadowDeviceNotFound;
        }

        if (newPorts.Count == 1)
        {
            connection = await GetConnectionAndDisableRuntime(newPorts[0]);
            newPort = newPorts[0];
        }
        else
        {
            foreach (var port in newPorts)
            {
                try
                {
                    connection = await GetConnectionAndDisableRuntime(port);
                    newPort = port;
                    break;
                }
                catch
                {
                    throw CommandException.MeadowDeviceNotFound;
                }
            }
        }

        Logger?.LogInformation($"{Strings.MeadowFoundAt} {newPort}");

        await connection!.WaitForMeadowAttach();

        // configure the route to that port for the user
        Settings.SaveSetting(SettingsManager.PublicSettings.Route, newPort);

        return connection;
    }

    private async Task<IMeadowConnection?> WriteRuntime(IMeadowConnection? connection, DeviceInfo? deviceInfo, FirmwarePackage package)
    {
        Logger?.LogInformation($"{Environment.NewLine}{Strings.GettingRuntimeFor} {package.Version}...");

        if (package.Runtime == null) { return null; }

        // get the path to the runtime file
        var rtpath = package.GetFullyQualifiedPath(package.Runtime);

        return await WriteRuntime(connection, deviceInfo, rtpath, package.Runtime);
    }

    private async Task<IMeadowConnection?> WriteRuntime(IMeadowConnection? connection, DeviceInfo? deviceInfo, string runtimePath, string destinationFilename)
    {
        connection ??= await GetConnectionAndDisableRuntime();

        Logger?.LogInformation($"{Environment.NewLine}{Strings.WritingRuntime}...");

        deviceInfo ??= await connection.GetDeviceInfo(CancellationToken);

        if (deviceInfo == null)
        {
            throw new CommandException(Strings.UnableToGetDeviceInfo);
        }

        if (UseDfu || RequiresDfuForRuntimeUpdates(deviceInfo))
        {
            var initialPorts = await MeadowConnectionManager.GetSerialPorts();

        write_runtime:
            if (!await connection!.Device!.WriteRuntime(runtimePath, CancellationToken))
            {
                // TODO: implement a retry timeout
                Logger?.LogInformation($"{Strings.ErrorWritingRuntime} - {Strings.Retrying}");
                goto write_runtime;
            }

            connection = await GetCurrentConnection(true);

            if (connection == null)
            {
                var newPort = await WaitForNewSerialPort(initialPorts) ?? throw CommandException.MeadowDeviceNotFound;
                connection = await GetCurrentConnection(true);

                Logger?.LogInformation($"{Strings.MeadowFoundAt} {newPort}");

                // configure the route to that port for the user
                Settings.SaveSetting(SettingsManager.PublicSettings.Route, newPort);
            }
        }
        else
        {
            await connection.Device!.WriteFile(runtimePath, $"/{AppManager.MeadowRootFolder}/update/os/{destinationFilename}");
        }

        return connection;
    }

    private async Task WriteEspFiles(IMeadowConnection? connection, DeviceInfo? deviceInfo, FirmwarePackage package)
    {
        connection ??= await GetConnectionAndDisableRuntime();

        if (connection == null) { return; } // couldn't find a connected device

        Logger?.LogInformation($"{Environment.NewLine}{Strings.WritingCoprocessorFiles}...");

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

        foreach (var file in fileList)
        {
            if (!File.Exists(file))
            {
                throw new CommandException(string.Format(Strings.InvalidFirmwareForSpecifiedPath, file), CommandExitCode.FileNotFound);
            }
        }

        deviceInfo ??= await connection.GetDeviceInfo(CancellationToken);

        if (deviceInfo == null) { throw new CommandException(Strings.UnableToGetDeviceInfo); }

        if (UseDfu || RequiresDfuForEspUpdates(deviceInfo))
        {
            await connection.Device!.WriteCoprocessorFiles(fileList, CancellationToken);
        }
        else
        {
            foreach (var file in fileList)
            {
                await connection!.Device!.WriteFile(file, $"/{AppManager.MeadowRootFolder}/update/os/{Path.GetFileName(file)}");
                await Task.Delay(500);
            }
        }
    }

    private ILibUsbDevice? GetLibUsbDeviceForCurrentEnvironment(LibUsbProvider? provider)
    {
        provider ??= new LibUsbProvider();

        var devices = provider.GetDevicesInBootloaderMode();

        var meadowsInDFU = devices.Where(device => device.IsMeadow()).ToList();

        if (meadowsInDFU.Count == 0)
        {
            return null;
        }

        if (meadowsInDFU.Count == 1 || IgnoreSerialNumberForDfu(provider))
        {   //IgnoreSerialNumberForDfu is a macOS-specific hack for Mark's machine 
            return meadowsInDFU.FirstOrDefault();
        }

        throw new CommandException(Strings.MultipleDfuDevicesFound);
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
                Logger?.LogError(string.Format(Strings.SpecifiedFirmwareVersionNotFound, Version));
                return null;
            }
            package = existing;
        }
        else
        {
            Version = collection.DefaultPackage?.Version ?? throw new CommandException($"{Strings.NoDefaultVersionSet}. {Strings.UseCommandFirmwareDefault}.");

            package = collection.DefaultPackage;
        }

        return package;
    }

    private async Task WriteOsWithDfu(ILibUsbDevice libUsbDevice, string osFile, bool ignoreSerialNumber = false)
    {
        string serialNumber;

        try
        {   //validate device
            serialNumber = libUsbDevice.GetDeviceSerialNumber();
        }
        catch
        {
            throw new CommandException($"{Strings.FirmwareWriteFailed} - {Strings.UnableToReadSerialNumber} ({Strings.MakeSureDeviceisConnected})");
        }

        try
        {
            if (ignoreSerialNumber)
            {
                serialNumber = string.Empty;
            }

            await DfuUtils.FlashFile(
                osFile,
                serialNumber,
                logger: Logger,
                format: DfuUtils.DfuFlashFormat.ConsoleOut);
        }
        catch (ArgumentException)
        {
            throw new CommandException($"{Strings.FirmwareWriteFailed} - {Strings.IsDfuUtilInstalled} {Strings.RunMeadowDfuInstall}");
        }
        catch (Exception ex)
        {
            Logger?.LogError($"Exception type: {ex.GetType().Name}");

            // TODO: scope this to the right exception type for Win 10 access violation thing
            // TODO: catch the Win10 DFU error here and change the global provider configuration to "classic"
            Settings.SaveSetting(SettingsManager.PublicSettings.LibUsb, "classic");

            throw new CommandException("This machine requires an older version of LibUsb. The CLI settings have been updated, re-run the 'firmware write' command to update your device.");
        }
    }

    private async Task<IList<string>> WaitForNewSerialPorts(IList<string>? ignorePorts)
    {
        var ports = await MeadowConnectionManager.GetSerialPorts();

        var retryCount = 0;

        while (ports.Count == 0)
        {
            if (retryCount++ > 10)
            {
                throw new CommandException(Strings.NewMeadowDeviceNotFound);
            }
            await Task.Delay(500);
            ports = await MeadowConnectionManager.GetSerialPorts();
        }

        if (ignorePorts != null)
        {
            return ports.Except(ignorePorts).ToList();
        }
        return ports.ToList();
    }

    private async Task<string?> WaitForNewSerialPort(IList<string>? ignorePorts)
    {
        var ports = await WaitForNewSerialPorts(ignorePorts);

        return ports.FirstOrDefault();
    }

    private bool IgnoreSerialNumberForDfu(LibUsbProvider provider)
    {   //hack check for Mark's Mac
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var devices = provider.GetDevicesInBootloaderMode();

            if (devices.Count == 2)
            {
                if (devices[0].GetDeviceSerialNumber().Length > 12 || devices[1].GetDeviceSerialNumber().Length > 12)
                {
                    return true;
                }
            }
        }

        return false;
    }
}