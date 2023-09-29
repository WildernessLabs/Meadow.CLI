using CliFx.Attributes;
using Meadow.Hcom;
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

    protected override async ValueTask ExecuteCommand(IMeadowConnection connection, Hcom.IMeadowDevice device, CancellationToken cancellationToken)
    {
        Logger.LogInformation($"Reading file '{MeadowFile}' from device...\n");

        var data = await device.ReadFileString(MeadowFile, cancellationToken);

        Logger.LogInformation(data);
    }
}
