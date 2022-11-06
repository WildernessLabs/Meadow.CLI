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
    [Command("package list", Description = "List Meadow Packages")]
    public class ListCommand : ICommand
    {
        private readonly ILogger<LogoutCommand> _logger;

        public ListCommand(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<LogoutCommand>();
        }

        public async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            await Task.Yield();

            PackageManager manager = new PackageManager(_logger);
            await manager.ListPackages();

        }
    }
}