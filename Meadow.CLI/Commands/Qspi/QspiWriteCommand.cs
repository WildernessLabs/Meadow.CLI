﻿using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Qspi
{
    [Command("qspi write", Description = "Write a QSPI value to the Meadow")]
    public class QspiWriteCommand : MeadowSerialPortCommand
    {
        private readonly ILogger<QspiWriteCommand> _logger;

        [CommandOption("value",'v', Description = "The QSPI Value to write", IsRequired = true)]
        public int Value {get; init;}

        public QspiWriteCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory)
            : base(downloadManager, loggerFactory)
        {
            _logger = LoggerFactory.CreateLogger<QspiWriteCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            var cancellationToken = console.RegisterCancellationHandler();

            await Meadow.QspiWrite(Value, cancellationToken);
        }
    }
}
