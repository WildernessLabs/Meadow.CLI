using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement
{
    [Command("use port", Description = "Set the preferred serial port")]
    public class UsePortCommand : MeadowCommand
    {
        private readonly ILogger<UsePortCommand> _logger;

        [CommandParameter(0)]
        public string PortName { get; set; }

        public UsePortCommand(DownloadManager manager, ILoggerFactory loggerFactory)
            : base(manager, loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<UsePortCommand>();
        }

        public override ValueTask ExecuteAsync(IConsole console)
        {
            _logger.LogInformation($"Setting port to {PortName}");
            SettingsManager.SaveSetting(Setting.PORT, PortName);
            return ValueTask.CompletedTask;
        }
    }
}
