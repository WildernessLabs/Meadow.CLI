using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Utility
{
    [Command("download os", Description = "Downloads the latest Meadow.OS to the host PC")]
    public class DownloadOsCommand : MeadowCommand
    {
        public DownloadOsCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory) : base(downloadManager, loggerFactory)
        {
        }

        [CommandOption("force", 'f', Description = "Force re-download of the OS", IsRequired = false)]
#if WIN_10
        public bool Force { get; } = false;
#else
        public bool Force { get; init; } = false;
#endif

        [CommandOption("osVersion", 'v', Description = "Download a specific OS version - x.x.x.x", IsRequired = false)]
#if WIN_10
        public string OsVersion { get; }
#else
        public string OsVersion { get; init; }
#endif

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            await base.ExecuteAsync(console);

            await DownloadManager.DownloadOsBinaries(OsVersion, Force);
        }
    }
}