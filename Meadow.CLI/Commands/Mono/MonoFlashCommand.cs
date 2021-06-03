using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Mono
{
    [Command("mono flash", Description = "Uploads the mono runtime files to the Meadow device. Does NOT move them into place.")]
    public class MonoFlashCommand : MeadowSerialCommand
    {
        private readonly ILogger<MonoRunStateCommand> _logger;

        public MonoFlashCommand(ILoggerFactory loggerFactory,
                                MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<MonoRunStateCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device = await MeadowDeviceManager
                                     .GetMeadowForSerialPort(
                                         SerialPortName,
                                         cancellationToken)
                                     .ConfigureAwait(false);

            await device.MonoFlashAsync(cancellationToken)
                                       .ConfigureAwait(false);

            _logger.LogInformation($"Mono Flashed Successfully");
        }
    }
}