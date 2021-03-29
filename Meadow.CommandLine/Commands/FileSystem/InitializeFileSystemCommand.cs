using System;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.NewDeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CommandLine.Commands.FileSystem
{
    [Command("filesystem init", Description = "Mount the File System on the Meadow Board")]
    public class InitializeFileSystemCommand : MeadowSerialCommand
    {
#if USE_PARTITIONS
        [CommandOption("Partition", 'p', Description = "The partition to write to on the Meadow")]
#endif
        public int Partition { get; init; } = 0;

        private readonly ILogger<InitializeFileSystemCommand> _logger;
        public InitializeFileSystemCommand(ILoggerFactory loggerFactory,
                                             Utils utils,
                                             MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, utils, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<InitializeFileSystemCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            _logger.LogInformation($"Initializing filesystem in partition {Partition}");

            using var device = await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, true, cancellationToken)
                                                        .ConfigureAwait(false);

            await device.InitializeFileSystem(Partition, cancellationToken).ConfigureAwait(false);
        }

        
    }
}