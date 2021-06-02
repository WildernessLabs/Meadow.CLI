using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.FileSystem
{
    [Command("filesystem mount", Description = "Mount the File System on the Meadow Board")]
    public class MountFileSystemCommand : MeadowSerialCommand
    {
#if USE_PARTITIONS
        [CommandOption("Partition", 'p', Description = "The partition to write to on the Meadow")]
#endif
        public int Partition { get; init; } = 0;

        private readonly ILogger<MountFileSystemCommand> _logger;

        public MountFileSystemCommand(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<MountFileSystemCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            _logger.LogInformation($"Mounting partition {Partition}");

            using var device = await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, cancellationToken)
                                                        .ConfigureAwait(false);

            await device.MountFileSystemAsync(Partition, cancellationToken)
                                   .ConfigureAwait(false);
        }
    }
}