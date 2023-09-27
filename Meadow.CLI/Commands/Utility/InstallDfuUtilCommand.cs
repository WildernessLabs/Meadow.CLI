using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.Utility
{
    [Command("install dfu-util", Description = "Install the DfuUtil utility")]
    public class InstallDfuUtilCommand : MeadowCommand
    {
        private readonly ILogger<InstallDfuUtilCommand> _logger;
        public InstallDfuUtilCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory) :base(downloadManager, loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<InstallDfuUtilCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();
            await base.ExecuteAsync(console);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if(IsAdministrator())
                {
                    await DownloadManager.InstallDfuUtil(Environment.Is64BitOperatingSystem, cancellationToken);
                }
                else
                {
                    _logger.LogInformation("To install dfu-util on Windows you'll need to open a Command Prompt or Terminal as an administrator and re-run the `meadow install dfu-util` command again.");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _logger.LogInformation("To install on macOS, run: brew install dfu-util");
            } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _logger.LogInformation(
                    "To install on Linux, use the package manager to install the dfu-util package");
            }
        }

        private static bool IsAdministrator()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            else
            {
                return false;
            }
        }
    }
}