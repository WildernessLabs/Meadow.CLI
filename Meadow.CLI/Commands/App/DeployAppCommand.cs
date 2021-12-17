using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;

namespace Meadow.CLI.Commands.App
{
    [Command("app deploy", Description = "Deploy the specified app to the Meadow")]
    public class DeployAppCommand : MeadowSerialCommand
    {
        [CommandOption(
            "file",
            'f',
            Description = "The path to the application to deploy to the app",
            IsRequired = true)]
        public string File { get; init; }

        [CommandOption("includePdbs", 'i', Description = "Include the PDB files on deploy to enable debugging", IsRequired = false)]
        public bool IncludePdbs { get; init; } = true;

        public DeployAppCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
        {
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);
            var cancellationToken = console.RegisterCancellationHandler();

            //check the device OS version, in order to download matching assemblies to it
            var deviceInfo = await Meadow.GetDeviceInfoAsync(TimeSpan.FromSeconds(30), cancellationToken).ConfigureAwait(false);
            string osVersion = deviceInfo.MeadowOsVersion.Split(' ')[0]; // we want the first part of e.g. '0.5.3.0 (Oct 13 2021 13:39:12)'

            var logger = LoggerFactory.CreateLogger<DownloadManager>();

            await new DownloadManager(logger).DownloadLatestAsync(osVersion).ConfigureAwait(false);

            await Meadow.DeployAppAsync(File, IncludePdbs, cancellationToken)
                        .ConfigureAwait(false);
        }
    }
}
