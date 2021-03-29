using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.NewDeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CommandLine.Commands.Files
{
    [Command("files write", Description = "Write files to the Meadow File System")]
    public class WritesFileCommand : MeadowSerialCommand
    {
        [CommandOption(
            "files",
            'f',
            Description = "The file(s) to write to the Meadow Files System")]
        public IList<string> Files { get; init; }

        [CommandOption(
            "targetFiles",
            't',
            Description = "The filename(s) to use on the Meadow File System")]
        public IList<string> TargetFileNames { get; init; }

#if USE_PARTITIONS
        [CommandOption("Partition", 'p', Description = "The partition to write to on the Meadow")]
#endif
        public int Partition { get; init; } = 0;


        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device = await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, true, cancellationToken).ConfigureAwait(false);
            if (Files.Count != TargetFileNames.Count)
            {
                await console.Output.WriteLineAsync(
                                 $"Number of files to write ({Files.Count}) does not match the number of target file names ({TargetFileNames.Count}).")
                             .ConfigureAwait(false);

                return;
            }

            for (var i = 0; i < Files.Count; i++)
            {
                var targetFileName = TargetFileNames[i];

                if (string.IsNullOrEmpty(targetFileName))
                {
                    targetFileName = new FileInfo(Files[i]).Name;
                }

                if (!File.Exists(Files[i]))
                {
                    await console.Output.WriteLineAsync($"Cannot find {Files[i]}")
                                 .ConfigureAwait(false);
                }
                else
                {
                    if (string.IsNullOrEmpty(targetFileName))
                    {
                        await console.Output.WriteLineAsync(
                                         $"Writing {Files[i]} to partition {Partition}")
                                     .ConfigureAwait(false);
                    }
                    else
                    {
                        await console.Output.WriteLineAsync(
                                         $"Writing {Files[i]} as {targetFileName} to partition {Partition}")
                                     .ConfigureAwait(false);
                    }

                    await device.WriteFile(Files[i],
                                           targetFileName,
                                           Partition,
                                           cancellationToken)
                                           .ConfigureAwait(false);
                }
            }
        }

        internal WritesFileCommand(ILoggerFactory loggerFactory, Utils utils, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, utils, meadowDeviceManager)
        {
        }
    }
}