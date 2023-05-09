using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
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
        PackageManager _packageManager;

        public CreateCommand(ILoggerFactory loggerFactory, PackageManager packageManager)
        {
            _logger = loggerFactory.CreateLogger<LogoutCommand>();
            _packageManager = packageManager;
        }

        [CommandOption("applicationPath", 'a', Description = "The path to the application directory", IsRequired = false)]
        public string ApplicationPath { get; init; }

        [CommandOption("osVersion", 'v', Description = "Version of Meadow OS to include in package", IsRequired = false)]
        public string OsVersion { get; init; }


        public async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            await Task.Yield();

            try
            {
                var zipFile = _packageManager.CreatePackage(ApplicationPath, OsVersion);
                if(!string.IsNullOrEmpty(zipFile))
                {
                    _logger.LogInformation($"{zipFile} created.");
                }           
            }
            catch(ArgumentException ex)
            {
                _logger.LogError(ex.Message);
            }
        }
    }
}