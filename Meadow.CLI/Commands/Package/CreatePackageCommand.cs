using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.Managers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Meadow.CLI.Commands.Cloud
{
    [Command("package create", Description = "Create Meadow Package")]
    public class CreateCommand : ICommand
    {
        private readonly ILogger<LogoutCommand> _logger;

        [CommandOption(
            "applicationPath",
            'a',
            Description = "The path to the application directory",
            IsRequired = true)]
        public string ApplicationPath { get; init; }

        [CommandOption("osVersion", 'v', Description = "Version of Meadow OS to include in package", IsRequired = true)]
        public string OsVersion { get; init; }

        public CreateCommand(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<LogoutCommand>();
        }

        public async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            await Task.Yield();

            PackageManager manager = new PackageManager(_logger);
            var zipFile = manager.CreatePackage(ApplicationPath, OsVersion);

            
        }
    }
}