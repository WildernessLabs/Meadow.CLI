using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;
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

            var osVersion = await Meadow.GetOSVersion(TimeSpan.FromSeconds(30), cancellationToken);

            try
            {
                await new DownloadManager(LoggerFactory).DownloadLatestAsync(osVersion);
            }
            catch
            {   //OS binaries failed to download
                //Either no internet connection or we're depoying to a pre-release OS version 
                console.Output.WriteLine("Meadow assemblies download failed, using local copy");
            }

            await Meadow.DeployAppAsync(File, IncludePdbs, cancellationToken);
        }
    }
}