using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.Internals.Dfu;
using Meadow.Hcom;
using Meadow.Software;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

public enum FirmwareType
{
    OS,
    Runtime,
    ESP
}

[Command("firmware write", Description = "Download a firmware package")]
public class FirmwareWriteCommand : BaseDeviceCommand<FirmwareWriteCommand>
{
    [CommandOption("version", 'v', IsRequired = false)]
    public string? Version { get; set; }

    [CommandOption("use-dfu", 'd', IsRequired = false, Description = "Force using DFU for writing the OS.")]
    public bool UseDfu { get; set; }

    [CommandParameter(0, Name = "Files to write", IsRequired = false)]
    public FirmwareType[]? Files { get; set; } = default!;

    public FirmwareWriteCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    public override async ValueTask ExecuteAsync(IConsole console)
    {
        var package = await GetSelectedPackage();

        if (Files == null)
        {
            Logger.LogInformation($"Writing all firmware for version '{package.Version}'...");

            Files = new FirmwareType[]
                {
                    FirmwareType.OS,
                    FirmwareType.Runtime,
                    FirmwareType.ESP
                };
        }

        if (!Files.Contains(FirmwareType.OS) && UseDfu)
        {
            Logger.LogError($"DFU is only used for OS files.  Select an OS file or remove the DFU option");
            return;
        }

        bool deviceSupportsOta = false; // TODO: get this based on device OS version

        if (package.OsWithoutBootloader == null
            || !deviceSupportsOta
            || UseDfu)
        {
            UseDfu = true;
        }


        if (UseDfu && Files.Contains(FirmwareType.OS))
        {
            // no connection is required here - in fact one won't exist
            // unless maybe we add a "DFUConnection"?
            await WriteOsWithDfu(package.GetFullyQualifiedPath(package.OSWithBootloader));

            // TODO: if the user requested flashing more than the OS, we have to wait for a connection and then proceed with that
            if (Files.Any(f => f != FirmwareType.OS))
            {
                var connection = ConnectionManager.GetCurrentConnection();
                if (connection == null)
                {
                    Logger.LogError($"No connection path is defined");
                    return;
                }

                await connection.WaitForMeadowAttach();

                var cancellationToken = console.RegisterCancellationHandler();
                await ExecuteCommand(connection, connection.Device, cancellationToken);
            }
        }
        else
        {
            await base.ExecuteAsync(console);
        }
    }

    private async Task<FirmwarePackage?> GetSelectedPackage()
    {
        var manager = new FileManager();
        await manager.Refresh();

        var collection = manager.Firmware["Meadow F7"];
        FirmwarePackage package;

        if (Version != null)
        {
            // make sure the requested version exists
            var existing = collection.FirstOrDefault(v => v.Version == Version);

            if (existing == null)
            {
                Logger.LogError($"Requested version '{Version}' not found.");
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

    protected override async ValueTask ExecuteCommand(IMeadowConnection connection, Hcom.IMeadowDevice device, CancellationToken cancellationToken)
    {
        var package = await GetSelectedPackage();

        var wasRuntimeEnabled = await device.IsRuntimeEnabled(cancellationToken);

        if (wasRuntimeEnabled)
        {
            Logger.LogInformation("Disabling device runtime...");
            await device.RuntimeDisable();
        }

        connection.FileWriteProgress += (s, e) =>
        {
            var p = (e.completed / (double)e.total) * 100d;
            Console.Write($"Writing {e.fileName}: {p:0}%     \r");
        };

        if (Files.Contains(FirmwareType.OS))
        {
            if (UseDfu)
            {
                // this would have already happened before now (in ExecuteAsync) so ignore
            }
            else
            {
                Logger.LogInformation($"{Environment.NewLine}Writing OS {package.Version}...");

                throw new NotSupportedException("OtA writes for the OS are not yet supported");
            }
        }
        if (Files.Contains(FirmwareType.Runtime))
        {
            Logger.LogInformation($"{Environment.NewLine}Writing Runtime {package.Version}...");

            // get the path to the runtime file
            var rtpath = package.GetFullyQualifiedPath(package.Runtime);

            // TODO: for serial, we must wait for the flash to complete

            await device.WriteRuntime(rtpath, cancellationToken);
        }
        if (Files.Contains(FirmwareType.ESP))
        {
            Logger.LogInformation($"{Environment.NewLine}Writing Coprocessor files...");

            var fileList = new string[]
                {
                        package.GetFullyQualifiedPath(package.CoprocApplication),
                        package.GetFullyQualifiedPath(package.CoprocBootloader),
                        package.GetFullyQualifiedPath(package.CoprocPartitionTable),
                };

            await device.WriteCoprocessorFiles(fileList, cancellationToken);
        }

        Logger.LogInformation($"{Environment.NewLine}");

        if (wasRuntimeEnabled)
        {
            await device.RuntimeEnable();
        }

        // TODO: if we're an F7 device, we need to reset
    }

    private async Task WriteOsWithDfu(string osFile)
    {
        await DfuUtils.FlashFile(
            osFile,
            logger: Logger,
            format: DfuUtils.DfuFlashFormat.ConsoleOut);
    }
}

