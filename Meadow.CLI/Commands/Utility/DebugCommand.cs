﻿using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Utility
{
    [Command("debug", Description = "Debug a Meadow Application")]
    public class DebugCommand : MeadowSerialPortCommand
    {
        private readonly ILogger<InstallDfuUtilCommand> _logger;

        public DebugCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory)
            : base(downloadManager, loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<InstallDfuUtilCommand>();
        }

        // VS 2019 - 4024
        // VS 2017 - 4022
        // VS 2015 - 4020
        [CommandOption("DebugPort", 'p', Description = "The port to run the debug server on")]
        public int Port { get; init; } = 4024;

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();
            using (var server = await Meadow.StartDebuggingSession(Port, cancellationToken))
            {
                _logger.LogInformation("Debugging server started. Press enter to exit");
                await console.Input.ReadLineAsync();
            }
        }
    }
}