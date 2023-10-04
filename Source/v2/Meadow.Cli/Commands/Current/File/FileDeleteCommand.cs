using CliFx.Attributes;
using Meadow.Hcom;
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
        if (CurrentConnection != null)
        {
            var fileList = await CurrentConnection.GetFileList(false);
            var exists = fileList?.Any(f => Path.GetFileName(f.Name) == MeadowFile) ?? false;

            if (!exists)
            {
                Logger?.LogError($"File '{MeadowFile}' not found on device.");
            }
            else
            {
                var wasRuntimeEnabled = await CurrentConnection.Device.IsRuntimeEnabled(CancellationToken);

                if (wasRuntimeEnabled)
                {
                    Logger?.LogError($"The runtime must be disabled before doing any file management. Use 'meadow runtime disable' first.");
                    return;
                }

                Logger?.LogInformation($"Deleting file '{MeadowFile}' from device...");
                await CurrentConnection.Device.DeleteFile(MeadowFile, CancellationToken);
            }
        }
    }
}
