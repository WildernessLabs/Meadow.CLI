﻿using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Nsh
{
    [Command("nsh enable", Description = "Enables NSH on the Meadow device.")]
    public class NshEnableCommand : MeadowSerialCommand
    {
        private readonly ILogger<NshEnableCommand> _logger;
        public NshEnableCommand(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<NshEnableCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            using var device =
                await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, cancellationToken).ConfigureAwait(false);
            
            await device.NshEnableAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
