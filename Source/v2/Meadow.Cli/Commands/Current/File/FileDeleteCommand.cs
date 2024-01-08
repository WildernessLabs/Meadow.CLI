using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("file delete", Description = "Deletes a file from the device")]
public class FileDeleteCommand : BaseDeviceCommand<FileDeleteCommand>
{
    [CommandParameter(0, Name = "MeadowFile", IsRequired = true)]
    public string MeadowFile { get; set; } = default!;

    public FileDeleteCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
    }

    protected override async ValueTask ExecuteCommand()
    {
        var connection = await GetCurrentConnection();

        if (connection == null)
        {
            return;
        }

        if (connection != null)
        {
            var fileList = await connection.GetFileList("/meadow0/", false);

            if (MeadowFile == "all")
            {
                foreach (var f in fileList)
                {
                    var p = Path.GetFileName(f.Name);
                    Logger?.LogInformation($"Deleting file '{p}' from device...");
                    await connection.Device.DeleteFile(p, CancellationToken);
                }
            }
            else
            {
                var exists = fileList?.Any(f => Path.GetFileName(f.Name) == MeadowFile) ?? false;

                if (!exists)
                {
                    Logger?.LogError($"File '{MeadowFile}' not found on device.");
                }
                else
                {
                    var wasRuntimeEnabled = await connection.Device.IsRuntimeEnabled(CancellationToken);

                    if (wasRuntimeEnabled)
                    {
                        Logger?.LogError($"The runtime must be disabled before doing any file management. Use 'meadow runtime disable' first.");
                        return;
                    }

                    Logger?.LogInformation($"Deleting file '{MeadowFile}' from device...");
                    await connection.Device.DeleteFile(MeadowFile, CancellationToken);
                }
            }
        }
    }
}
