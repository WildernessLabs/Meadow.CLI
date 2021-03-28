using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.NewDeviceManagement;

namespace Meadow.CommandLine.Commands.DeviceManagement
{
    [Command("flash esp", Description = "Flash the ESP co-processor")]
    public class FlashEspCommand : MeadowSerialCommand
    {
        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device = await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, cancellationToken: cancellationToken).ConfigureAwait(false);

            await Utils.FlashEsp(console, device, cancellationToken)
                .ConfigureAwait(false);

            await Utils.ResetMeadow(console, device, cancellationToken).ConfigureAwait(false);

        }
    }
}
