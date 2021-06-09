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
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            await Meadow.MonoEnableAsync(cancellationToken)
                        .ConfigureAwait(false);
            _logger.LogInformation("Mono Enabled Successfully");
        }
    }
}