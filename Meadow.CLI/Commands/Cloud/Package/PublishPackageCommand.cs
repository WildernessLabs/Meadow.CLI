using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.CloudServices;
using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Identity;
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
    [Command("package publish", Description = "List Meadow Packages")]
    public class PublishCommand : ICommand
    {
        private readonly ILogger<LogoutCommand> _logger;
        PackageService _packageService;

        public PublishCommand(ILoggerFactory loggerFactory, PackageService packageService)
        {
            _logger = loggerFactory.CreateLogger<LogoutCommand>();
            _packageService = packageService;
        }
        
        [CommandOption("packageId", 'p', Description = "ID of the package to publish", IsRequired = true)]
#if WIN_10
        public string PackageId { get; }
#else
        public string PackageId { get; init; }
#endif
        [CommandOption("collectionId", 'c', Description = "The target collection for publishing", IsRequired = true)]
        public string CollectionId { get; set; }
        [CommandOption("metadata", 'm', Description = "Pass through metadata", IsRequired = false)]
        public string Metadata { get; set; }
        [CommandOption("host", Description = "Optionally set a host (default is https://www.meadowcloud.co)", IsRequired = false)]
        public string Host { get; set; }

        public async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            await Task.Yield();

            try
            {
                await _packageService.PublishPackage(PackageId, CollectionId, Metadata, Host, cancellationToken);
                _logger.LogInformation("Publish successful.");
            }
            catch(MeadowCloudException mex)
            {
                _logger.LogInformation($"Publish failed: {mex.Message}");
            }
        }
    }
}