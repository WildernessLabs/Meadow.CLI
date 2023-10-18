using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("file read", Description = "Reads a file from the device and writes it to the local file system")]
public class FileReadCommand : BaseDeviceCommand<FileReadCommand>
{
    [CommandParameter(0, Name = "MeadowFile", IsRequired = true)]
    public string MeadowFile { get; set; } = default!;

    [CommandParameter(1, Name = "LocalFile", IsRequired = false)]
    public string? LocalFile { get; set; }

    public FileReadCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        await base.ExecuteCommand();

        if (Connection != null && Connection.Device != null)
        {
            Logger?.LogInformation($"Getting file '{MeadowFile}' from device...");

            var success = await Connection.Device.ReadFile(MeadowFile, LocalFile, CancellationToken);

            if (success)
            {
                Logger?.LogInformation($"Success");
            }
            else
            {
                Logger?.LogInformation($"Failed to retrieve file");
            }
        }
    }
}
