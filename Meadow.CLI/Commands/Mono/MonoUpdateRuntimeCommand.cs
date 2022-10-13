using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Mono
{
    [Command("mono update rt", Description = "Uploads the mono runtime to the Meadow device, erases the runtime flash and copies the runtime binary from the file partition to the runtime flash")]
    public class MonoUpdateRuntimeCommand : MeadowSerialCommand
    {
        [CommandOption("filename",'f', Description = "The local name of the mono runtime file - Default is empty")]
        public string Filename {get; init;}

        [CommandOption("osVersion", 'v', Description = "Flash the mono runtime from a specific downloaded OS version - x.x.x.x")]
        public string OSVersion { get; init; }

        private readonly ILogger<MonoRunStateCommand> _logger;

        public MonoUpdateRuntimeCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory)
            : base(downloadManager, loggerFactory)
        {
            _logger = LoggerFactory.CreateLogger<MonoRunStateCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            await Meadow.UpdateMonoRuntime(Filename, OSVersion, cancellationToken: cancellationToken);

            await Meadow.ResetMeadow(cancellationToken);

            _logger.LogInformation("Mono flashed successfully");
        }
    }
}