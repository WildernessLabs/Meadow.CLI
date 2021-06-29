using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Mono
{
    [Command("mono flash", Description = "Uploads the mono runtime file to the Meadow device. Does NOT move it into place")]
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
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            await Meadow.MonoFlashAsync(cancellationToken)
                        .ConfigureAwait(false);

            _logger.LogInformation($"Mono Flashed Successfully");
        }
    }
}