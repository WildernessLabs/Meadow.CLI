using System;
using System.Diagnostics.Metrics;
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
            // in order to delete, the runtime must be disabled
            var wasRuntimeEnabled = await Connection.IsRuntimeEnabled();

            if (wasRuntimeEnabled)
            {
                Logger?.LogInformation("Disabling runtime...");

                await Connection.RuntimeDisable(CancellationToken);
            }

            try
            {
                var fileList = await Connection.GetFileList(false);

                if (MeadowFile == "all")
                {
                    if (Console != null)
                    {
                        Logger?.LogInformation($"{Environment.NewLine}Are you sure you want to delete ALL files from this device (Y/N)?");

                        var reply = await Console.Input.ReadLineAsync();
                        if ((!string.IsNullOrEmpty(reply) && reply.ToLower() != "y") || string.IsNullOrEmpty(reply))
                        {
                            return;
                        }
                    }

                    if (fileList != null)
                    {
                        if (fileList.Length > 0)
                        {
                            foreach (var f in fileList)
                            {
                                if (Connection.Device != null)
                                {
                                    var p = Path.GetFileName(f.Name);

                                    Console?.Output.WriteAsync($"Deleting file '{p}' from device...         \r");
                                    await Connection.Device.DeleteFile(p, CancellationToken);
                                }
                                else
                                {
                                    Logger?.LogError($"No Device Found.");
                                }
                            }
                        }
                        else
                        {
                            Logger?.LogInformation($"No files to delete.");
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
                            Console?.Output.WriteAsync($"Deleting file '{MeadowFile}' from device...         \r");
                            await Connection.Device.DeleteFile(MeadowFile, CancellationToken);
                        }
                    }
                }
            }
            finally
            {
                if (wasRuntimeEnabled)
                {
                    // restore runtime state
                    Logger?.LogInformation("Enabling runtime...");

                    await Connection.RuntimeEnable(CancellationToken);
                }
            }
        }
    }
}