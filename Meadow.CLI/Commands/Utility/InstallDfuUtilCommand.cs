using System;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI;
using Meadow.CLI.Core;
using Microsoft.Extensions.Logging;

namespace Meadow.CommandLine.Commands.Utility
{
    [Command("install dfu-util", Description = "Install the DfuUtil utility")]
    public class InstallDfuUtilCommand : ICommand
    {
        private readonly ILogger<InstallDfuUtilCommand> _logger;
        public InstallDfuUtilCommand(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<InstallDfuUtilCommand>();
        }

        public ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            if (OperatingSystem.IsWindows() && IsAdministrator())
            {
                var downloadManager = new DownloadManager();
                downloadManager.InstallDfuUtil(Environment.Is64BitOperatingSystem);
            }
            else if (OperatingSystem.IsMacOS())
            {
                _logger.LogInformation("To install on macOS, run: brew install dfu-util");
            } else if (OperatingSystem.IsLinux())
            {
                _logger.LogInformation(
                    "To install on Linux, use the package manager to install the dfu-util package");
            }
            return ValueTask.CompletedTask;
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
