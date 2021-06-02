using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Storage
{
    [Command("flash erase", Description = "Erase the flash on the Meadow Board")]
    public class EraseFlashCommand : MeadowSerialCommand
    {
        private readonly ILogger<EraseFlashCommand> _logger;

        public EraseFlashCommand(ILoggerFactory loggerFactory,
                                   MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<EraseFlashCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            _logger.LogInformation("Erasing flash.");
            using var device = await MeadowDeviceManager
                                     .GetMeadowForSerialPort(
                                         SerialPortName,
                                         cancellationToken)
                                     .ConfigureAwait(false);

            await device.EraseFlash(cancellationToken)
                        .ConfigureAwait(false);
        }
    }
}