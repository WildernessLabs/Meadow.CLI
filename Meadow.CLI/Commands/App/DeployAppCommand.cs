using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
#if WIN_10
        public string File { get; }
#else
        public string File { get; init; }
#endif

        [CommandOption(
            "nolink",
            'n',
            Description = "A list of assemblies to skip linking (trimming) on",
            IsRequired = false)]
#if WIN_10
        public IList<string> NoLink { get; } = null;
#else
        public IList<string> NoLink { get; init; } = null;
#endif

        [CommandOption("includePdbs", 'i', Description = "Include the PDB files on deploy to enable debugging", IsRequired = false)]
#if WIN_10
        public bool IncludePdbs { get; } = true;
#else
        public bool IncludePdbs { get; init; } = true;
#endif

        public DeployAppCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
        {
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);
            var cancellationToken = console.RegisterCancellationHandler();

            // check to see if the app exists before continuing
            if (!System.IO.File.Exists(File))
            {
                Logger.LogError($"Application file '{File}' not found");
            }

            // what OS version is on the target?
            var osVersion = await Meadow.GetOSVersion(TimeSpan.FromSeconds(30), cancellationToken);

            try
            {
                // make sure we have the same locally because we will do linking/trimming against that runtime
                await new DownloadManager(LoggerFactory).DownloadOsBinaries(osVersion);
            }
            catch
            {   //OS binaries failed to download
                //Either no internet connection or we're depoying to a pre-release OS version 
                console.Output.WriteLine("Meadow assemblies download failed, using local copy");
            }

            await Meadow.DeployApp(fileName: File, includePdbs: IncludePdbs, noLink: NoLink, verbose: Verbose, cancellationToken: cancellationToken);
        }
    }
}