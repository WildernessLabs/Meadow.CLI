using System;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.NewDeviceManagement;

namespace Meadow.CommandLine.Commands.FileSystem
{
    [Command("filesystem format", Description = "Format a File System on the Meadow Board")]
    public class FormatFileSystemCommand : MeadowSerialCommand
    {
#if USE_PARTITIONS
        [CommandOption("Partition", 'p', Description = "The partition to write to on the Meadow")]
#endif
        public int Partition { get; init; } = 0;
        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            await console.Output.WriteLineAsync($"Formatting file system on partition {Partition}")
                         .ConfigureAwait(false);
            using var device = await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName)
                                                        .ConfigureAwait(false);

            await device.CreateFileSystem(cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}