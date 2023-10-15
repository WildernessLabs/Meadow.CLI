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
        await base.ExecuteCommand();

        if (Connection != null)
        {
            var fileList = await Connection.GetFileList(false);

            if (MeadowFile == "all")
            {
                if (fileList != null)
                {
                    foreach (var f in fileList)
                    {
                        if (Connection.Device != null)
                        {
                            var p = Path.GetFileName(f.Name);

                            Logger?.LogInformation($"Deleting file '{p}' from device...");
                            await Connection.Device.DeleteFile(p, CancellationToken);
                        }
                        else
                        {
                            Logger?.LogError($"No Device Found.");
                        }
                    }
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
                    if (Connection.Device != null)
                    {
                        var wasRuntimeEnabled = await Connection.Device.IsRuntimeEnabled(CancellationToken);

                        if (wasRuntimeEnabled)
                        {
                            Logger?.LogError($"The runtime must be disabled before doing any file management. Use 'meadow runtime disable' first.");
                            return;
                        }

                        Logger?.LogInformation($"Deleting file '{MeadowFile}' from device...");
                        await Connection.Device.DeleteFile(MeadowFile, CancellationToken);
                    }
                }
            }
        }
    }
}