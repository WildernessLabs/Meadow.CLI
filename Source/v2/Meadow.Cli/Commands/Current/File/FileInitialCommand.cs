using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("file initial", Description = "Display the initial bytes from a device file")]
public class FileInitialCommand : BaseDeviceCommand<FileInitialCommand>
{
    [CommandParameter(0, Name = "MeadowFile", IsRequired = true)]
    public string MeadowFile { get; init; } = default!;

    public FileInitialCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        var device = await GetCurrentDevice();

        Logger?.LogInformation($"Reading file '{MeadowFile}' from device...\n");

        var data = await device.ReadFileString(AppTools.SanitiseMeadowFilename(MeadowFile), CancellationToken);

        if (data == null)
        {
            Logger?.LogError($"Failed to retrieve file");
            return;
        }

        Logger?.LogInformation(data);
    }
}