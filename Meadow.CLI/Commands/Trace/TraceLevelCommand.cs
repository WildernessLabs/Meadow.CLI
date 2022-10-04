using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Trace
{
    [Command("trace level", Description = "Enable trace logging on the Meadow")]
    public class TraceLevelCommand : MeadowSerialCommand
    {
        private readonly ILogger<TraceLevelCommand> _logger;

        public TraceLevelCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<TraceLevelCommand>();
        }

        [CommandOption("TraceLevel",'t', Description = "The desired trace level")]
        public uint TraceLevel { get; init; }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            await Meadow.SetTraceLevel(TraceLevel, cancellationToken);
        }
    }
}
