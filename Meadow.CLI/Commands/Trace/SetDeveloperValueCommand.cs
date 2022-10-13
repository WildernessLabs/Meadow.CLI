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

        public SetDeveloperValueCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory)
            : base(downloadManager, loggerFactory)
        {
            _logger = LoggerFactory.CreateLogger<SetDeveloperValueCommand>();
        }

        [CommandOption("developer", 'd', Description = "The developer value to set. Valid values are 1 - 4")]
        public uint Developer { get; set; }

        [CommandOption("value", 'v', Description = "The value to apply to the developer value. Valid values are 0 to 4,294,967,295")]
        public uint Value { get; set; }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            var task = Developer switch
            {
                1 => Meadow.SetDeveloper1(Value, cancellationToken),
                2 => Meadow.SetDeveloper2(Value, cancellationToken),
                3 => Meadow.SetDeveloper3(Value, cancellationToken),
                4 => Meadow.SetDeveloper4(Value, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(Developer), Developer, "Valid values are 1 - 4")
            };

            await task;
        }
    }
}
