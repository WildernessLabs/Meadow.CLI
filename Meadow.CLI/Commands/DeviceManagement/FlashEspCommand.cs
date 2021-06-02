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
            var cancellationToken = console.RegisterCancellationHandler();

            using var device = await MeadowDeviceManager
                                     .GetMeadowForSerialPort(
                                         SerialPortName,
                                         cancellationToken)
                                     .ConfigureAwait(false);

            await device.FlashEspAsync(cancellationToken)
                       .ConfigureAwait(false);

            await device.ResetMeadowAsync(cancellationToken)
                       .ConfigureAwait(false);
        }
    }
}