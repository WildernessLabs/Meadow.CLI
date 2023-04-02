﻿using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.CloudServices;
using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Identity;
using Microsoft.Extensions.Configuration;
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
        UserService _userService;
        PackageService _packageService;
        IConfiguration _config;

        public UploadCommand(ILoggerFactory loggerFactory, UserService userService, PackageService packageService, IConfiguration config)
        {
            _logger = loggerFactory.CreateLogger<LogoutCommand>();
            _userService = userService;
            _packageService = packageService;
            _config = config;
        }

        [CommandOption("mpakPath", 'p', Description = "The full path of the mpak file", IsRequired = true)]
        public string MpakPath { get; init; }

        [CommandOption("orgId", 'o', Description = "OrgId to upload to", IsRequired = false)]
        public string OrgId { get; set; }

        [CommandOption("description", 'd', Description = "Description of the package", IsRequired = false)]
        public string Description { get; set; }

        public async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            await Task.Yield();

            try
            {
                var userOrgs = await _userService.GetUserOrgs(cancellationToken).ConfigureAwait(false);
                if (!userOrgs.Any())
                {
                    _logger.LogInformation($"Please visit {_config["meadowCloudHost"]} to register your account.");
                    return;
                }
                else if (userOrgs.Count() > 1 && string.IsNullOrEmpty(OrgId))
                {
                    _logger.LogInformation($"Please specify the orgId.");
                    return;
                }
                else if (userOrgs.Count() == 1 && string.IsNullOrEmpty(OrgId))
                {
                    OrgId = userOrgs.First().OrgId;
                }

                if (!userOrgs.Select(x => x.OrgId).Contains(OrgId))
                {
                    _logger.LogInformation($"Invalid orgId: {OrgId}");
                    return;
                }

            }
            catch (MeadowCloudAuthException ex)
            {
                _logger.LogInformation($"You must be signed in to execute this command.");
                return;
            }

            try
            {
                var package = await _packageService.UploadPackage(MpakPath, OrgId, Description, cancellationToken);
                _logger.LogInformation($"Upload complete. Package Id: {package.Id}");
            }
            catch (MeadowCloudException mex)
            {
                _logger.LogError($"Upload failed: {mex.Message}");
            }
        }
    }
}