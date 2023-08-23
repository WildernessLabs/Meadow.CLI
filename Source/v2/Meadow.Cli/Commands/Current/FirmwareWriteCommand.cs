using CliFx.Attributes;
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

    [CommandParameter(0, Name = "Files to write", IsRequired = false)]
    public FirmwareType[]? Files { get; set; } = default!;

    public FirmwareWriteCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand(IMeadowConnection connection, Hcom.IMeadowDevice device, CancellationToken cancellationToken)
    {
        var manager = new FileManager();
        await manager.Refresh();

        // for now we only support F7
        // TODO: add switch and support for other platforms
        var collection = manager.Firmware["Meadow F7"];
        FirmwarePackage package;

        if (Version != null)
        {
            // make sure the requested version exists
            var existing = collection.FirstOrDefault(v => v.Version == Version);

            if (existing == null)
            {
                Logger.LogError($"Requested version '{Version}' not found.");
                return;
            }
            package = existing;
        }
        else
        {
            Version = collection.DefaultPackage?.Version ??
                throw new Exception("No default version set");

            package = collection.DefaultPackage;
        }

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

        if (Files == null)
        {
            Logger.LogInformation($"Writing all firmware for version '{Version}'...");
        }
        else
        {
            if (Files.Contains(FirmwareType.OS))
            {
                Logger.LogInformation($"{Environment.NewLine}Writing OS...");
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
        }

        Logger.LogInformation($"{Environment.NewLine}");

        if (wasRuntimeEnabled)
        {
            await device.RuntimeEnable();
        }

        // TODO: if we're an F7 device, we need to reset
    }

    private void Connection_FileWriteProgress(object? sender, (long completed, long total) e)
    {
        throw new NotImplementedException();
    }
}

