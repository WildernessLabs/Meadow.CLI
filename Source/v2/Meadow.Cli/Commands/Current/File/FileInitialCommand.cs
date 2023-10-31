using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("file initial", Description = "Display the initial bytes from a device file")]
public class FileInitialCommand : BaseDeviceCommand<FileInitialCommand>
{
    [CommandParameter(0, Name = "MeadowFile", IsRequired = true)]
    public string MeadowFile { get; set; } = default!;

    public FileInitialCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        await base.ExecuteCommand();

        if (Connection != null)
        {
            if (Connection.Device != null)
            {
                Logger?.LogInformation($"Reading file '{MeadowFile}' from device...\n");

                var data = await Connection.Device.ReadFileString(MeadowFile, CancellationToken);

                Logger?.LogInformation(data);
            }
        }
    }
}