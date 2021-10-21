﻿using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Utility
{
    [Command("download os", Description = "Downloads the latest Meadow.OS to the host PC")]
    public class DownloadOsCommand : MeadowCommand
    {
        private readonly ILogger<InstallDfuUtilCommand> _logger;
        public DownloadOsCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory) : base(downloadManager, loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<InstallDfuUtilCommand>();
        }

        [CommandOption("force", 'f', Description = "Force re-download of the OS", IsRequired = false)]
        public bool force { get; init; } = false;

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();
            await base.ExecuteAsync(console);

            await DownloadManager.DownloadLatestAsync(null, force).ConfigureAwait(false);
        }
    }
}
