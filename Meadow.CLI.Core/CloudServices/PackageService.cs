﻿using Meadow.CLI.Core.Identity;
using MeadowCLI;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Meadow.CLI.Core.Exceptions;
using System.Threading;
using Meadow.CLI.Core.CloudServices.Messages;
using Microsoft.Extensions.Configuration;

namespace Meadow.CLI.Core.CloudServices
{
    public class PackageService : CloudServiceBase
    {
        IConfiguration _config;
        IdentityManager _identityManager;

        public PackageService(IConfiguration config, IdentityManager identityManager) : base(identityManager)
        {
            _config = config;
            _identityManager = identityManager;
        }

        public async Task<Package> UploadPackage(string mpakPath, string orgId, string description, CancellationToken cancellationToken)
        {
            if (!File.Exists(mpakPath))
            {
                throw new ArgumentException($"Invalid path: {mpakPath}");
            }
            var fi = new FileInfo(mpakPath);

            var hasher = new Hasher();
            var crc = await hasher.CalculateCrc32(mpakPath);

            var httpClient = await AuthenticatedHttpClient();

            var uploadFilename = $"{Path.GetFileName(mpakPath)}";

            using (var multipartFormContent = new MultipartFormDataContent())
            {
                var fileStreamContent = new StreamContent(File.OpenRead(mpakPath));
                fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                dynamic payload = new { 
                    orgId, 
                    description = description ?? "",
                    crc = crc ?? "",
                    fileSize = fi.Length
                };
                var json = JsonSerializer.Serialize<dynamic>(payload);

                multipartFormContent.Add(fileStreamContent, name: "file", fileName: uploadFilename);
                multipartFormContent.Add(new StringContent(json), "json");

                var response = await httpClient.PostAsync($"{_config["meadowCloudHost"]}/api/packages", multipartFormContent);
                if (response.IsSuccessStatusCode)
                {
                    var package = JsonSerializer.Deserialize<Package>(await response.Content.ReadAsStringAsync());
                    return package;
                }
                else
                {
                    var message = await response.Content.ReadAsStringAsync();
                    throw new MeadowCloudException($"{response.StatusCode} {message}");
                }
            }
        }

        public async Task PublishPackage(string packageId, CancellationToken cancellationToken)
        {
            var authToken = await _identityManager.GetAccessToken(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(authToken))
            {
                throw new MeadowCloudAuthException();
            }

            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            var response = await httpClient.PostAsync($"{_config["meadowCloudHost"]}/api/packages/{packageId}/publish", null);

            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync();
                throw new MeadowCloudException(message);
            }
        }

        public async Task<List<Package>> GetOrgPackages(string orgId, CancellationToken cancellationToken)
        {
            var authToken = await _identityManager.GetAccessToken(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(authToken))
            {
                throw new MeadowCloudAuthException();
            }

            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authToken);

            var result = await httpClient.GetStringAsync($"{_config["meadowCloudHost"]}/api/orgs/{orgId}/packages");
            return JsonSerializer.Deserialize<List<Package>>(result);
        }
    }
}
