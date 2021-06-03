using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Mono
{
    [Command("mono enable", Description = "Sets mono to run on the Meadow board and then resets it.")]
    public class MonoEnableCommand : MeadowSerialCommand
    {
        private readonly ILogger<MonoEnableCommand> _logger;
        public MonoEnableCommand(ILoggerFactory loggerFactory,
                                 MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<MonoEnableCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device = await MeadowDeviceManager
                                     .GetMeadowForSerialPort(
                                         SerialPortName,
                                         cancellationToken)
                                     .ConfigureAwait(false);

            await device.MonoEnableAsync(cancellationToken)
                       .ConfigureAwait(false);
            _logger.LogInformation("Mono Enabled Successfully");
        }
    }
}