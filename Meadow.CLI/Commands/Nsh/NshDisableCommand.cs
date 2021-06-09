﻿using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Nsh
{
    [Command("nsh disable", Description = "Disables NSH on the Meadow device.")]
    public class NshDisableCommand : MeadowSerialCommand
    {
        private readonly ILogger<NshDisableCommand> _logger;
        public NshDisableCommand(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = LoggerFactory.CreateLogger<NshDisableCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            await Meadow.NshDisableAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
