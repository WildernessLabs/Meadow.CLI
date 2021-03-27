using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using MeadowCLI.DeviceManagement;

namespace Meadow.CommandLine.Commands.Storage
{
    [Command("flash partition", Description = "Partition the flash on the Meadow Board")]
    public class PartitionCommand : MeadowSerialCommand
    {
#if USE_PARTITIONS
        [CommandOption("NumberOfPartitions", 'p', Description = "The number of partitions to create on the Meadow")]
#endif
        public int NumberOfPartitions { get; init; } = 1;

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            await console.Output.WriteLineAsync(
                             $"Partitioning filesystem into {NumberOfPartitions} partition(s)")
                         .ConfigureAwait(false);

            using var device = await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName)
                                                        .ConfigureAwait(false);

            await MeadowFileManager.PartitionFileSystem(device, NumberOfPartitions)
                                   .ConfigureAwait(false);
        }
    }
}