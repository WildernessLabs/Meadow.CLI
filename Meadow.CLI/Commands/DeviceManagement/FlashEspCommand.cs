using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement
{
    [Command("flash esp", Description = "Flash the ESP co-processor")]
    public class FlashEspCommand : MeadowSerialCommand
    {
        public FlashEspCommand(ILoggerFactory loggerFactory,
                               MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);
            var cancellationToken = console.RegisterCancellationHandler();

            await Meadow.FlashEspAsync(cancellationToken: cancellationToken)
                        .ConfigureAwait(false);

            await Meadow.ResetMeadowAsync(cancellationToken)
                        .ConfigureAwait(false);
        }
    }
}