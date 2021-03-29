using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.NewDeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CommandLine.Commands.Files
{
    [Command("files delete", Description = "Delete files from the Meadow File System")]
    public class DeleteFileCommand : MeadowSerialCommand
    {
        [CommandOption(
            "files",
            'f',
            Description = "The file(s) to delete from the Meadow Files System")]
        public IList<string> Files { get; init; }

#if USE_PARTITIONS
        [CommandOption("Partition", 'p', Description = "The partition to write to on the Meadow")]
#endif
        public int Partition { get; init; } = 0;


        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();
            using var device = await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, true, cancellationToken).ConfigureAwait(false);
            foreach (var file in Files.Where(file => string.IsNullOrWhiteSpace(file) == false))
            {
                if (!string.IsNullOrEmpty(file))
                {
                    await console
                          .Output.WriteLineAsync($"Deleting {file} from partition {Partition}")
                          .ConfigureAwait(false);

                    await device.DeleteFile(file, Partition, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        internal DeleteFileCommand(ILoggerFactory loggerFactory, Utils utils, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, utils, meadowDeviceManager)
        {
        }
    }
}