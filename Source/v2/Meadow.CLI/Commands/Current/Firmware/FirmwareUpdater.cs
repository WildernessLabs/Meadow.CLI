using System.Runtime.InteropServices;
using Meadow.CLI.Core.Internals.Dfu;
using Meadow.Hcom;
using Meadow.LibUsb;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

public class FirmwareUpdater<T> where T : BaseDeviceCommand<T>
{
    private string? individualFile;
    private FirmwareType[]? firmwareFileTypes;
    private bool useDfu;
    private string? osVersion;
    private string? serialNumber;
    private readonly MeadowConnectionManager connectionManager;
    private readonly ILogger? logger;
    private readonly bool provisioningInProgress;
    private readonly CancellationToken cancellationToken;
    private readonly ISettingsManager settings;
    private readonly FileManager fileManager;

    private int lastWriteProgress = 0;

    private BaseDeviceCommand<T> command;

    public event EventHandler<(string message, double percentage)> UpdateProgress = default!;

    public FirmwareUpdater(BaseDeviceCommand<T> command, ISettingsManager settings, FileManager fileManager, MeadowConnectionManager connectionManager, string? individualFile, FirmwareType[]? firmwareFileTypes, bool useDfu, string? osVersion, string? serialNumber, ILogger? logger, CancellationToken cancellationToken)
    {
        this.command = command;
        this.settings = settings;
        this.fileManager = fileManager;
        this.connectionManager = connectionManager;
        this.individualFile = individualFile;
        this.firmwareFileTypes = firmwareFileTypes;
        this.useDfu = useDfu;
        this.osVersion = osVersion;
        this.serialNumber = serialNumber;
        this.logger = logger;
        this.provisioningInProgress = logger == null;
        this.cancellationToken = cancellationToken;
    }

    public async Task<bool> UpdateFirmware()
    {
        var package = await GetSelectedPackage();

        if (package == null)
        {
            return false;
        }

        if (individualFile != null)
        {
            // check the file exists
            var fullPath = Path.GetFullPath(individualFile);
            if (!File.Exists(fullPath))
            {
                throw new CommandException(string.Format(Strings.InvalidFirmwareForSpecifiedPath, fullPath), CommandExitCode.FileNotFound);
            }

            // set the file type
            firmwareFileTypes = Path.GetFileName(individualFile) switch
            {
                F7FirmwarePackageCollection.F7FirmwareFiles.OSWithBootloaderFile => new[] { FirmwareType.OS },
                F7FirmwarePackageCollection.F7FirmwareFiles.OsWithoutBootloaderFile => new[] { FirmwareType.OS },
                F7FirmwarePackageCollection.F7FirmwareFiles.RuntimeFile => new[] { FirmwareType.Runtime },
                F7FirmwarePackageCollection.F7FirmwareFiles.CoprocApplicationFile => new[] { FirmwareType.ESP },
                F7FirmwarePackageCollection.F7FirmwareFiles.CoprocBootloaderFile => new[] { FirmwareType.ESP },
                F7FirmwarePackageCollection.F7FirmwareFiles.CoprocPartitionTableFile => new[] { FirmwareType.ESP },
                _ => throw new CommandException(string.Format(Strings.UnknownSpecifiedFirmwareFile, Path.GetFileName(individualFile)))
            };

            logger?.LogInformation(string.Format($"{Strings.WritingSpecifiedFirmwareFile}...", fullPath));
        }
        else if (firmwareFileTypes == null)
        {
            logger?.LogInformation(string.Format(Strings.WritingAllFirmwareForSpecifiedVersion, package.Version));

            firmwareFileTypes = new FirmwareType[]
            {
                FirmwareType.OS,
                FirmwareType.Runtime,
                FirmwareType.ESP
            };
        }
        else if (firmwareFileTypes.Length == 1 && firmwareFileTypes[0] == FirmwareType.Runtime)
        {   //use the "DFU" path when only writing the runtime
            useDfu = true;
        }

        IMeadowConnection? connection = null;
        DeviceInfo? deviceInfo = null;

        if (firmwareFileTypes.Contains(FirmwareType.OS))
        {
            UpdateProgress?.Invoke(this, (Strings.FirmwareUpdater.FlashingOS, 20));
            await WriteOSFiles(connection, deviceInfo, package, useDfu);
        }

        if (!string.IsNullOrWhiteSpace(serialNumber))
        {
            connection = await GetConnectionAndDisableRuntime(await MeadowConnectionManager.GetRouteFromSerialNumber(serialNumber));
            if (connection != null)
            {
                if (provisioningInProgress)
                {
                    connection.ConnectionMessage += (o, e) =>
                    {
                        UpdateProgress?.Invoke(this, (e, 0));
                    };
                }

                deviceInfo = await connection.GetDeviceInfo(cancellationToken);
            }
        }

        if (firmwareFileTypes.Contains(FirmwareType.Runtime) || Path.GetFileName(individualFile) == F7FirmwarePackageCollection.F7FirmwareFiles.RuntimeFile)
        {
            UpdateProgress?.Invoke(this, (Strings.FirmwareUpdater.WritingRuntime, 40));
            await WriteRuntimeFiles(connection, deviceInfo, package, individualFile);
        }

        if (firmwareFileTypes.Contains(FirmwareType.ESP)
             || Path.GetFileName(individualFile) == F7FirmwarePackageCollection.F7FirmwareFiles.CoprocPartitionTableFile
             || Path.GetFileName(individualFile) == F7FirmwarePackageCollection.F7FirmwareFiles.CoprocApplicationFile
             || Path.GetFileName(individualFile) == F7FirmwarePackageCollection.F7FirmwareFiles.CoprocBootloaderFile)
        {
            UpdateProgress?.Invoke(this, (Strings.FirmwareUpdater.WritingESP, 60));
            await WriteEspFiles(connection, deviceInfo, package);
        }

        // reset device
        if (connection != null && connection.Device != null)
        {
            await connection.Device.Reset();
        }

        return true;
    }

    private async Task WriteEspFiles(IMeadowConnection? connection, DeviceInfo? deviceInfo, FirmwarePackage package)
    {
        connection ??= await GetConnectionAndDisableRuntime(await MeadowConnectionManager.GetRouteFromSerialNumber(serialNumber));

        await WriteEsp(connection, deviceInfo, package);

        // reset device
        if (connection != null && connection.Device != null)
        {
            await connection.Device.Reset();
        }
    }

    private async Task WriteRuntimeFiles(IMeadowConnection? connection, DeviceInfo? deviceInfo, FirmwarePackage package, string? individualFile)
    {
        if (string.IsNullOrEmpty(individualFile))
        {
            connection = await WriteRuntime(connection, deviceInfo, package);
        }
        else
        {
            connection = await WriteRuntime(connection, deviceInfo, individualFile, Path.GetFileName(individualFile));
        }

        if (connection == null)
        {
            throw CommandException.MeadowDeviceNotFound;
        }

        await connection.WaitForMeadowAttach();
    }

    private async Task WriteOSFiles(IMeadowConnection? connection, DeviceInfo? deviceInfo, FirmwarePackage package, bool useDfu)
    {
        var osFileWithBootloader = package.GetFullyQualifiedPath(package.OSWithBootloader);
        var osFileWithoutBootloader = package.GetFullyQualifiedPath(package.OsWithoutBootloader);

        if (osFileWithBootloader == null && osFileWithoutBootloader == null)
        {
            throw new CommandException(string.Format(Strings.OsFileNotFoundForSpecifiedVersion, package.Version));
        }

        var provider = new LibUsbProvider();
        var dfuDevice = GetLibUsbDeviceForCurrentEnvironment(provider, serialNumber);
        bool ignoreSerial = IgnoreSerialNumberForDfu(provider);

        if (dfuDevice != null)
        {
            logger?.LogInformation($"{Strings.DfuDeviceDetected} - {Strings.UsingDfuToWriteOs}");
            useDfu = true;
        }
        else
        {
            if (useDfu)
            {
                throw new CommandException(Strings.NoDfuDeviceDetected);
            }

            connection = await GetConnectionAndDisableRuntime();

            deviceInfo = await connection.GetDeviceInfo(cancellationToken);
        }

        if (useDfu || dfuDevice != null || osFileWithoutBootloader == null || RequiresDfuForRuntimeUpdates(deviceInfo!))
        {
            // get a list of ports - it will not have our meadow in it (since it should be in DFU mode)
            var initialPorts = await MeadowConnectionManager.GetSerialPorts();

            await WriteOsWithDfu(dfuDevice!, osFileWithBootloader!, ignoreSerial);

            await Task.Delay(1500);

            connection ??= await FindMeadowConnection(initialPorts);

            await connection.WaitForMeadowAttach(cancellationToken);
        }
        else
        {
            await connection!.Device!.WriteFile(osFileWithoutBootloader, $"/{AppTools.MeadowRootFolder}/update/os/{package.OsWithoutBootloader}");
        }
    }

    private async Task<FirmwarePackage?> GetSelectedPackage()
    {
        await fileManager.Refresh();

        var collection = fileManager.Firmware["Meadow F7"];
        FirmwarePackage package;

        if (osVersion != null)
        {
            // make sure the requested version exists
            var existing = collection.FirstOrDefault(v => v.Version == osVersion);

            if (existing == null)
            {
                logger?.LogError(string.Format(Strings.SpecifiedFirmwareVersionNotFound, osVersion));
                return null;
            }
            package = existing;
        }
        else
        {
            osVersion = collection.DefaultPackage?.Version ?? throw new CommandException($"{Strings.NoDefaultVersionSet}. {Strings.UseCommandFirmwareDefault}.");

            package = collection.DefaultPackage;
        }

        return package;
    }

    private ILibUsbDevice? GetLibUsbDeviceForCurrentEnvironment(LibUsbProvider? provider, string? serialNumber = null)
    {
        provider ??= new LibUsbProvider();

        var devices = provider.GetDevicesInBootloaderMode();

        var meadowsInDFU = devices.Where(device => device.IsMeadow()).ToList();

        if (meadowsInDFU.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(serialNumber))
        {
            return meadowsInDFU.Where(device => device.SerialNumber == serialNumber).FirstOrDefault();
        }
        else if (meadowsInDFU.Count == 1 || IgnoreSerialNumberForDfu(provider))
        {   //IgnoreSerialNumberForDfu is a macOS-specific hack for Mark's machine 
            return meadowsInDFU.FirstOrDefault();
        }

        throw new CommandException(Strings.MultipleDfuDevicesFound);
    }

    private bool IgnoreSerialNumberForDfu(LibUsbProvider provider)
    {   //hack check for Mark's Mac
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var devices = provider.GetDevicesInBootloaderMode();

            if (devices.Count == 2)
            {
                if (devices[0].SerialNumber.Length > 12 || devices[1].SerialNumber.Length > 12)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private async Task<IMeadowConnection> GetConnectionAndDisableRuntime(string? route = null)
    {
        IMeadowConnection connection;

        if (!string.IsNullOrWhiteSpace(route))
        {
            connection = await command.GetConnectionForRoute(route, true);
        }
        else
        {
            connection = await command.GetCurrentConnection(true);
        }

        if (await connection.Device!.IsRuntimeEnabled())
        {
            logger?.LogInformation($"{Strings.DisablingRuntime}...");
            await connection.Device.RuntimeDisable();
        }

        lastWriteProgress = 0;

        connection.FileWriteProgress += (s, e) =>
        {
            var p = (int)(e.completed / (double)e.total * 100d);
            // don't report < 10% increments (decrease spew on large files)
            if (p - lastWriteProgress < 10) { return; }

            lastWriteProgress = p;

            logger?.LogInformation($"{Strings.Writing} {e.fileName}: {p:0}%     {(p < 100 ? string.Empty : "\r\n")}");
        };
        connection.DeviceMessageReceived += (s, e) =>
        {
            if (e.message.Contains("% downloaded"))
            {   // don't echo this, as we're already reporting % written
            }
            else
            {
                logger?.LogInformation(e.message);
            }
        };
        connection.ConnectionMessage += (s, message) =>
        {
            logger?.LogInformation(message);
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

    private async Task WriteOsWithDfu(ILibUsbDevice libUsbDevice, string osFile, bool ignoreSerialNumber = false)
    {
        try
        {   //validate device
            if (string.IsNullOrWhiteSpace(serialNumber))
            {
                serialNumber = libUsbDevice.SerialNumber;
            }
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
                logger: logger,
                format: provisioningInProgress ? DfuUtils.DfuFlashFormat.None : DfuUtils.DfuFlashFormat.ConsoleOut);
        }
        catch (ArgumentException)
        {
            throw new CommandException($"{Strings.FirmwareWriteFailed} - {Strings.IsDfuUtilInstalled} {Strings.RunMeadowDfuInstall}");
        }
        catch (Exception ex)
        {
            logger?.LogError($"Exception type: {ex.GetType().Name}");

            // TODO: scope this to the right exception type for Win 10 access violation thing
            // TODO: catch the Win10 DFU error here and change the global provider configuration to "classic"
            settings.SaveSetting(SettingsManager.PublicSettings.LibUsb, "classic");

            throw new CommandException(Strings.FirmwareUpdater.SwitchingToLibUsbClassic);
        }
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

        logger?.LogInformation($"{Strings.MeadowFoundAt} {newPort}");

        await connection!.WaitForMeadowAttach();

        // configure the route to that port for the user
        settings.SaveSetting(SettingsManager.PublicSettings.Route, newPort);

        return connection;
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

    private async Task<IMeadowConnection?> WriteRuntime(IMeadowConnection? connection, DeviceInfo? deviceInfo, FirmwarePackage package)
    {
        logger?.LogInformation($"{Environment.NewLine}{Strings.GettingRuntimeFor} {package.Version}...");

        if (package.Runtime == null) { return null; }

        // get the path to the runtime file
        var rtpath = package.GetFullyQualifiedPath(package.Runtime);

        return await WriteRuntime(connection, deviceInfo, rtpath, package.Runtime);
    }

    private async Task<IMeadowConnection?> WriteRuntime(IMeadowConnection? connection, DeviceInfo? deviceInfo, string runtimePath, string destinationFilename)
    {
        connection ??= await GetConnectionAndDisableRuntime(await MeadowConnectionManager.GetRouteFromSerialNumber(serialNumber));

        logger?.LogInformation($"{Environment.NewLine}{Strings.WritingRuntime}...");

        deviceInfo ??= await connection.GetDeviceInfo(cancellationToken);

        if (deviceInfo == null)
        {
            throw new CommandException(Strings.UnableToGetDeviceInfo);
        }

        if (useDfu || RequiresDfuForRuntimeUpdates(deviceInfo))
        {
            var initialPorts = await MeadowConnectionManager.GetSerialPorts();

        write_runtime:
            if (!await connection!.Device!.WriteRuntime(runtimePath, cancellationToken))
            {
                // TODO: implement a retry timeout
                logger?.LogInformation($"{Strings.ErrorWritingRuntime} - {Strings.Retrying}");
                goto write_runtime;
            }

            connection ??= await command.GetCurrentConnection(true);

            if (connection == null)
            {
                var newPort = await WaitForNewSerialPort(initialPorts) ?? throw CommandException.MeadowDeviceNotFound;
                connection = await command.GetCurrentConnection(true);

                logger?.LogInformation($"{Strings.MeadowFoundAt} {newPort}");

                // configure the route to that port for the user
                settings.SaveSetting(SettingsManager.PublicSettings.Route, newPort);
            }
        }
        else
        {
            await connection.Device!.WriteFile(runtimePath, $"/{AppTools.MeadowRootFolder}/update/os/{destinationFilename}");
        }

        return connection;
    }

    private async Task WriteEsp(IMeadowConnection? connection, DeviceInfo? deviceInfo, FirmwarePackage package)
    {
        connection ??= await GetConnectionAndDisableRuntime(await MeadowConnectionManager.GetRouteFromSerialNumber(serialNumber));

        if (connection == null) { return; } // couldn't find a connected device

        logger?.LogInformation($"{Environment.NewLine}{Strings.WritingCoprocessorFiles}...");

        string[] fileList;

        if (individualFile != null)
        {
            fileList = new string[] { individualFile };
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

        deviceInfo ??= await connection.GetDeviceInfo(cancellationToken);

        if (deviceInfo == null) { throw new CommandException(Strings.UnableToGetDeviceInfo); }

        if (useDfu || RequiresDfuForEspUpdates(deviceInfo))
        {
            await connection.Device!.WriteCoprocessorFiles(fileList, cancellationToken);
        }
        else
        {
            foreach (var file in fileList)
            {
                await connection!.Device!.WriteFile(file, $"/{AppTools.MeadowRootFolder}/update/os/{Path.GetFileName(file)}");
            }
        }
    }

    private bool RequiresDfuForEspUpdates(DeviceInfo info)
    {
        return true;
    }
}