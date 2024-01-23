using System;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Trace
{
    [Command("set developer", Description = "Set developer value")]
    public class SetDeveloperValueCommand : MeadowSerialCommand
    {
        private readonly ILogger<SetDeveloperValueCommand> _logger;

        public SetDeveloperValueCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<SetDeveloperValueCommand>();
        }

        [CommandOption("developer", 'd', Description = "The developer value to set.")]
        public ushort DeveloperLevel { get; set; }

        [CommandOption("value", 'v', Description = "The value to apply to the developer value. Valid values are 0 to 4,294,967,295")]
        public uint Value { get; set; }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            try
            {

                await Meadow.SetDeveloper(DeveloperLevel, Value, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error Setting Developer : {ex.Message}");
            }
        }
    }
}
