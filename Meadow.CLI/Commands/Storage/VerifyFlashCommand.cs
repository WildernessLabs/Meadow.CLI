using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Storage
{
    [Command("flash verify", Description = "Erase the flash on the Meadow Board")]
    public class VerifyFlashCommand : MeadowSerialCommand
    {
        private readonly ILogger<VerifyFlashCommand> _logger;
        public VerifyFlashCommand(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<VerifyFlashCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            _logger.LogInformation("Verifying flash");
            using var device = await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, cancellationToken).ConfigureAwait(false);
            await device.VerifyErasedFlash(cancellationToken).ConfigureAwait(false);
        }
    }
}
