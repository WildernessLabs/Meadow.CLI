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
        public uint Developer { get; set; }

        [CommandOption("value", 'v', Description = "The value to apply to the developer value.")]
        public uint Value { get; set; }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            /*var task = Developer switch
            {
                1 => Meadow.SetDeveloper1(Value, cancellationToken),
                2 => Meadow.SetDeveloper2(Value, cancellationToken),
                3 => Meadow.SetDeveloper3(Value, cancellationToken),
                4 => Meadow.SetDeveloper4(Value, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(Developer), Developer, "Valid values are 1 - 4")
            };*/

            try
            {
                await Meadow.SetDeveloper(Developer, Value, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error Setting Developer : {ex.Message}");
            }
        }
    }
}
