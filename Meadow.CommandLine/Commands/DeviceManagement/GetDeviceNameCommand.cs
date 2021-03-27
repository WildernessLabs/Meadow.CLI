using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using MeadowCLI.DeviceManagement;

namespace Meadow.CommandLine.Commands.DeviceManagement
{
    [Command("device name", Description = "Get the name of the Meadow")]
    public class GetDeviceNameCommand : MeadowSerialCommand
    {
        public override async ValueTask ExecuteAsync(IConsole console)
        {
            using var device =
                await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName).ConfigureAwait(false);

            await MeadowDeviceManager.GetDeviceName(device).ConfigureAwait(false);
        }
    }
}
