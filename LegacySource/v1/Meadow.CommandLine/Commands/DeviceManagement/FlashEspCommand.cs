﻿using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.NewDeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CommandLine.Commands.DeviceManagement
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
                                         true,
                                         cancellationToken)
                                     .ConfigureAwait(false);

            await device.FlashEsp(cancellationToken)
                       .ConfigureAwait(false);

            await device.ResetMeadow(cancellationToken)
                       .ConfigureAwait(false);
        }
    }
}