using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Trace
{
    [Command("trace enable", Description = "Enable trace logging on the Meadow")]
    public class TraceEnableCommand : MeadowSerialCommand
    {
        private readonly ILogger<TraceEnableCommand> _logger;

        public TraceEnableCommand(ILoggerFactory loggerFactory,
                                  MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<TraceEnableCommand>();
        }

        [CommandOption("Level", 'l', Description = "The desired trace level")]
        public uint? TraceLevel { get; init; }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            _logger.LogInformation("Enabling trace.");
            await Meadow.TraceEnableAsync(cancellationToken)
                        .ConfigureAwait(false);

            if (TraceLevel.HasValue)
            {
                _logger.LogInformation("Setting trace level");
                await Meadow.SetTraceLevelAsync(TraceLevel.Value, cancellationToken)
                            .ConfigureAwait(false);
            }
            _logger.LogInformation("Trace enabled successfully");
        }
    }
}