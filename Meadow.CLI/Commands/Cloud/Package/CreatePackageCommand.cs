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

        [CommandOption("projectFilePath", 'p', Description = "The path to the project file (ie .csproj)",
            IsRequired = true)]
        public string ProjectPath { get; init; } = default!;

        [CommandOption("osVersion", 'v', Description = "Target OS version for the app", IsRequired = true)]
        public string OsVersion { get; init; } = default!;

        [CommandOption("name", 'n', Description = "Name of the mpak file to be created", IsRequired = false)]
        public string MpakName { get; init; } = default!;

        [CommandOption("filter", 'f', Description = "Glob pattern to filter files. ex ('app.dll', 'app*','{app.dll,meadow.dll}')",
            IsRequired = false)]
        public string FilesToInclude { get; init; } = "*";

        public async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            await Task.Yield();

            try
            {
                var mpak = await _packageManager.CreatePackage(ProjectPath, OsVersion, MpakName, FilesToInclude);
                if (!string.IsNullOrEmpty(mpak))
                {
                    _logger.LogInformation($"{mpak} created.");
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex.Message);
            }
        }
    }
}