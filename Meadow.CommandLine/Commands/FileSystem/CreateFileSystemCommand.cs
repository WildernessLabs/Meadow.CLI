using System;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using MeadowCLI.DeviceManagement;

namespace Meadow.CommandLine.Commands.FileSystem
{
    [Command("filesystem create", Description = "Create a File System on the Meadow Board")]
    public class CreateFileSystemCommand : MeadowSerialCommand
    {
        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            await console.Output.WriteLineAsync("Creating a file system on the Meadow.");
            using var device = await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName)
                                                        .ConfigureAwait(false);

            await MeadowFileManager.CreateFileSystem(device).ConfigureAwait(false);
        }
    }
}