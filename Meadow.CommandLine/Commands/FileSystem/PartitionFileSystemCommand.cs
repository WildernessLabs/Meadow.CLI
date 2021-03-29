using System;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.NewDeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CommandLine.Commands.FileSystem
{
    [Command("filesystem partition", Description = "Create a File System on the Meadow Board")]
    public class PartitionFileSystemCommand : MeadowSerialCommand
    {

#if USE_PARTITIONS
        [CommandOption("NumberOfPartitions", 'p', Description = "The number of partitions to create on the Meadow")]
#endif
        public int NumberOfPartitions { get; init; } = 1;

        private readonly ILogger<PartitionFileSystemCommand> _logger;
        public PartitionFileSystemCommand(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<PartitionFileSystemCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            _logger.LogInformation(
                $"Partitioning filesystem into {NumberOfPartitions} partition(s)");

            using var device = await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, true, cancellationToken)
                                                        .ConfigureAwait(false);

            await device.PartitionFileSystem(NumberOfPartitions, cancellationToken)
                        .ConfigureAwait(false);
        }
    }
}