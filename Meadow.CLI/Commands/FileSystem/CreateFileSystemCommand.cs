using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.FileSystem
{
    [Command("filesystem create", Description = "Initializes the file system on the Meadow device, including creating the appropriate partitions and formatting.")]
    public class CreateFileSystemCommand : MeadowSerialCommand
    {
        public CreateFileSystemCommand(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            await console.Output.WriteLineAsync("Creating a file system on the Meadow.");
            using var device = await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, cancellationToken)
                                                        .ConfigureAwait(false);

            await device.CreateFileSystemAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}