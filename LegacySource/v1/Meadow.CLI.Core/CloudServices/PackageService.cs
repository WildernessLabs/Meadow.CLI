using Meadow.CLI.Core.CloudServices.Messages;
using Meadow.CLI.Core.DeviceManagement.Tools;
using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Identity;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.CLI.Core.CloudServices
{
    public class PackageService : CloudServiceBase
    {
        readonly IConfiguration _config;
        private string _info_json = "info.json";

        public PackageService(IConfiguration config, IdentityManager identityManager) : base(identityManager)
        {
            _config = config;
        }

        public async Task<Package> UploadPackage(string mpakPath, string orgId, string description, string host,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(host))
            {
                host = _config[Constants.MEADOW_CLOUD_HOST_CONFIG_NAME];
            }

            if (!File.Exists(mpakPath))
            {
                throw new ArgumentException($"Invalid path: {mpakPath}");
            }

            var fi = new FileInfo(mpakPath);

            var osVersion = GetPackageOsVersion(fi.FullName);

            var httpClient = await GetAuthenticatedHttpClient(cancellationToken);

            using var multipartFormContent = new MultipartFormDataContent();

            var fileStreamContent = new StreamContent(File.OpenRead(mpakPath));
            fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var crcFileHash = await CrcTools.CalculateCrc32FileHash(mpakPath);

            dynamic payload = new
            {
                orgId,
                description = description ?? "",
                crc = crcFileHash ?? "",
                fileSize = fi.Length,
                osVersion
            };
            var json = JsonSerializer.Serialize<dynamic>(payload);

            multipartFormContent.Add(fileStreamContent, name: "file", fileName: fi.Name);
            multipartFormContent.Add(new StringContent(json), "json");

            var response = await httpClient.PostAsync($"{host}/api/packages", multipartFormContent);
            if (response.IsSuccessStatusCode)
            {
                var package = JsonSerializer.Deserialize<Package>(await response.Content.ReadAsStringAsync());
                return package!;
            }
            else
            {
                var message = await response.Content.ReadAsStringAsync();
                throw new MeadowCloudException($"{response.StatusCode} {message}");
            }
        }

        private string GetPackageOsVersion(string packagePath)
        {
            var result = string.Empty;

            var tempFolder = System.IO.Path.GetTempPath();
            var tempInfoJson = Path.Combine(tempFolder, $"{Guid.NewGuid().ToString("N")}.zip");

            using ZipArchive zip = ZipFile.Open(packagePath, ZipArchiveMode.Read);
            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                if (entry.Name == _info_json)
                {
                    entry.ExtractToFile(tempInfoJson);
                }
            }

            if (File.Exists(tempInfoJson))
            {
                var content = File.ReadAllText(tempInfoJson);
                var packageInfo = JsonSerializer.Deserialize<PackageInfo>(content);
                result = packageInfo.OsVersion;
                File.Delete(tempInfoJson);
            }

            return result;
        }

        public async Task PublishPackage(string packageId, string collectionId, string metadata, string host,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(host))
            {
                host = _config[Constants.MEADOW_CLOUD_HOST_CONFIG_NAME];
            }

            var httpClient = await GetAuthenticatedHttpClient(cancellationToken);

            var payload = new { metadata, collectionId };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response =
                await httpClient.PostAsync($"{host}/api/packages/{packageId}/publish", content, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var message = await response.Content.ReadAsStringAsync();
                throw new MeadowCloudException(message);
            }
        }

        public async Task<List<Package>> GetOrgPackages(string orgId, string host, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(host))
            {
                host = _config[Constants.MEADOW_CLOUD_HOST_CONFIG_NAME];
            }

            var httpClient = await GetAuthenticatedHttpClient(cancellationToken);

            var result = await httpClient.GetStringAsync($"{host}/api/orgs/{orgId}/packages");

            return JsonSerializer.Deserialize<List<Package>>(result) ?? new List<Package>();
        }
    }
}