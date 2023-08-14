using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Trace
{
    [Command("trace enable", Description = "Enable trace logging on the Meadow")]
    public class TraceEnableCommand : MeadowSerialCommand
    {
        private readonly ILogger<TraceEnableCommand> _logger;

        public TraceEnableCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<TraceEnableCommand>();
        }

        [CommandOption("Level", 'l', Description = "The desired trace level")]
#if WIN_10
        public uint? TraceLevel { get; }
#else
        public uint? TraceLevel { get; init; }
#endif

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            _logger.LogInformation("Enabling trace.");
            await Meadow.TraceEnable(cancellationToken);

            if (TraceLevel.HasValue)
            {
                _logger.LogInformation("Setting trace level");
                await Meadow.SetTraceLevel(TraceLevel.Value, cancellationToken);
            }
            _logger.LogInformation("Trace enabled successfully");
        }
    }
}