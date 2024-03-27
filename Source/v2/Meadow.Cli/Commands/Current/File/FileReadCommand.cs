using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("file read", Description = "Reads a file from the device and writes it to the local file system")]
public class FileReadCommand : BaseDeviceCommand<FileReadCommand>
{
    [CommandParameter(0, Name = "MeadowFile", IsRequired = true)]
    public string MeadowFile { get; init; } = default!;

    [CommandParameter(1, Name = "LocalFile", IsRequired = false)]
    public string? LocalFile { get; init; }

    public FileReadCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        var device = await GetCurrentDevice();
        var connection = await GetCurrentConnection();

        var received = 0;

        connection.FileBytesReceived += (s, count) =>
        {
            received += count;
            Logger?.LogInformation($"Received {received} bytes");
        };

        connection.FileReadCompleted += (s, f) =>
        {
            Logger?.LogInformation($"File written to '{f}'");
        };

        Logger?.LogInformation($"Getting file '{MeadowFile}' from device...");
        var runtimeIsEnabled = await device.IsRuntimeEnabled();

        if (runtimeIsEnabled)
        {
            Logger?.LogInformation($"Disabling runtime...");
            await device.RuntimeDisable();
        }

        var success = await device.ReadFile(MeadowFile, LocalFile, CancellationToken);

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