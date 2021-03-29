using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.NewDeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CommandLine.Commands.Mono
{
    [Command("mono disable", Description = "Disable Mono on the Meadow")]
    public class MonoDisableCommand : MeadowSerialCommand
    {
        private readonly ILogger<MonoDisableCommand> _logger;
        public MonoDisableCommand(ILoggerFactory loggerFactory, Utils utils, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, utils, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<MonoDisableCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device =
                await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, true, cancellationToken).ConfigureAwait(false);

            await Utils.DisableMono(device, cancellationToken).ConfigureAwait(false);
        }
    }
}
