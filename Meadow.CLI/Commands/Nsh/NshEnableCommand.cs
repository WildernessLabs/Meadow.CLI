﻿using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Nsh
{
    [Command("nsh enable", Description = "Enables NSH on the Meadow device")]
    public class NshEnableCommand : MeadowSerialPortCommand
    {
        private readonly ILogger<NshEnableCommand> _logger;
        
        public NshEnableCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory)
            : base(downloadManager, loggerFactory)
        {
            _logger = LoggerFactory.CreateLogger<NshEnableCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            await Meadow.NshEnable(cancellationToken);
        }
    }
}
