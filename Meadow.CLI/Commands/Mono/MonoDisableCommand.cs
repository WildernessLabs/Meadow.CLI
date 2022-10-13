using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Mono
{
    [Command("mono disable", Description = "Sets mono to NOT run on the Meadow board then resets it")]
    public class MonoDisableCommand : MeadowSerialCommand
    {
        private readonly ILogger<MonoDisableCommand> _logger;
        
        public MonoDisableCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory)
            : base(downloadManager, loggerFactory)
        {
            _logger = LoggerFactory.CreateLogger<MonoDisableCommand>();
        }

        [CommandOption("force",'f', Description = "Send the Mono Disable Command even if Mono is already disabled")]
        public bool Force { get; init; }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            await Meadow.MonoDisable(Force, cancellationToken);
            _logger.LogInformation("Mono Disabled Successfully");
        }
    }
}
