using System;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.NewDeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CommandLine.Commands.DeviceManagement
{
    [Command("device info", Description = "Get the device info")]
    public class GetDeviceInfoCommand : MeadowSerialCommand
    {
        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device =
                await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, true, cancellationToken).ConfigureAwait(false);

            var deviceInfo = await device.GetDeviceInfo(cancellationToken: cancellationToken).ConfigureAwait(false);
            await console.Output.WriteLineAsync(deviceInfo).ConfigureAwait(false);
        }

        internal GetDeviceInfoCommand(ILoggerFactory loggerFactory, Utils utils, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, utils, meadowDeviceManager)
        {
        }
    }
}
