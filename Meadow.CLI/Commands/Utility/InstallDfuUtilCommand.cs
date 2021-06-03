using System;
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
        public InstallDfuUtilCommand(ILoggerFactory loggerFactory) :base(loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<InstallDfuUtilCommand>();
        }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            if (OperatingSystem.IsWindows() && IsAdministrator())
            {
                var downloadManager = new DownloadManager(_logger);
                await downloadManager.InstallDfuUtilAsync(Environment.Is64BitOperatingSystem, cancellationToken);
            }
            else if (OperatingSystem.IsMacOS())
            {
                _logger.LogInformation("To install on macOS, run: brew install dfu-util");
            } else if (OperatingSystem.IsLinux())
            {
                _logger.LogInformation(
                    "To install on Linux, use the package manager to install the dfu-util package");
            }
        }

        [SupportedOSPlatform("windows")]
        private static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
