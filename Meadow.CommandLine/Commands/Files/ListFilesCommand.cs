using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using MeadowCLI.DeviceManagement;

namespace Meadow.CommandLine.Commands.Files
{
    [Command("files list", Description = "List files in the on-board filesystem")]
    public class ListFilesCommand : MeadowSerialCommand
    {
#if USE_PARTITIONS
        [CommandOption("Partition", 'p', Description = "The partition to list the files")]
#endif
        public int Partition{ get; init; } = 0;

        [CommandOption("includeCrcs", 'i', Description = "Include the CRCs of the files")]
        public bool IncludeCrcs { get; init; }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            await console.Output.WriteLineAsync(
                             $"Partitioning filesystem into {Partition} partition(s)")
                         .ConfigureAwait(false);

            using var device = await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName)
                                                        .ConfigureAwait(false);

            if (IncludeCrcs)
            {
                await MeadowFileManager.ListFilesAndCrcs(device, Partition)
                                       .ConfigureAwait(false);
            }
            else
            {
                await MeadowFileManager.ListFiles(device, Partition)
                                       .ConfigureAwait(false);
            }
        }
    }
}