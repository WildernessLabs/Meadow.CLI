using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.NewDeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CommandLine.Commands.Mono
{
    [Command("mono enable", Description = "Enable Mono on the Meadow Board")]
    public class MonoEnableCommand : MeadowSerialCommand
    {
        private readonly ILogger<MonoEnableCommand> _logger;
        public MonoEnableCommand(ILoggerFactory loggerFactory,
                                 Utils utils,
                                 MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, utils, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<MonoEnableCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device = await MeadowDeviceManager
                                     .GetMeadowForSerialPort(
                                         SerialPortName,
                                         true,
                                         cancellationToken)
                                     .ConfigureAwait(false);

            await Utils.EnableMono(device, cancellationToken)
                       .ConfigureAwait(false);
        }
    }
}