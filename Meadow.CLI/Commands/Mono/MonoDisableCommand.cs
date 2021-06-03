using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Mono
{
    [Command("mono disable", Description = "Disable Mono on the Meadow")]
    public class MonoDisableCommand : MeadowSerialCommand
    {
        private readonly ILogger<MonoDisableCommand> _logger;
        public MonoDisableCommand(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<MonoDisableCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device =
                await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, cancellationToken).ConfigureAwait(false);

            await device.MonoDisableAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Mono Disabled Successfully");
        }
    }
}
