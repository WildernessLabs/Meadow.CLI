using System.Diagnostics;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using MeadowCLI.DeviceManagement;

namespace Meadow.CommandLine.Commands.Mono
{
    [Command("mono disable", Description = "Disable Mono on the Meadow")]
    public class MonoDisableCommand : MeadowSerialCommand
    {
        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device =
                await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, true, cancellationToken).ConfigureAwait(false);

            await Utils.DisableMono(console, device, cancellationToken).ConfigureAwait(false);
        }
    }
}
