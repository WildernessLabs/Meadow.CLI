using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.Identity;
using Meadow.CLI.Core.Managers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Meadow.CLI.Commands.Cloud
{
    [Command("package upload", Description = "Upload Meadow Package")]
    public class UploadCommand : ICommand
    {
        private readonly ILogger<LogoutCommand> _logger;

        [CommandOption(
            "mpakPath",
            'p',
            Description = "The path to the mpak file",
            IsRequired = true)]
        public string MpakPath { get; init; }

        public UploadCommand(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<LogoutCommand>();
        }

        public async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();
            
            await Task.Yield();

            PackageManager manager = new PackageManager(_logger);
            await manager.UploadPackage(MpakPath);
        }
    }
}