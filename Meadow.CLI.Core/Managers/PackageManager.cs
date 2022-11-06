using Meadow.CLI.Core.Identity;
using MeadowCLI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Meadow.CLI.Core.Managers
{
    public class PackageManager
    {
        public PackageManager(ILogger logger)
        {
            _logger = logger;
        }

        private readonly ILogger _logger;

        public string CreatePackage(string applicationPath, string osVersion)
        {
            var zipFile = Path.Combine(Environment.CurrentDirectory, $"{DateTime.UtcNow.ToString("yyyyMMdd")}{DateTime.UtcNow.Millisecond.ToString()}.mpak");

            if (!Directory.Exists(applicationPath))
            {
                _logger.LogInformation($"\"{applicationPath}\" is not found. Please try again.");
            }

            var osFilePath = Path.Combine(DownloadManager.FirmwareDownloadsFilePathRoot, osVersion);
            if (!Directory.Exists(osFilePath))
            {
                _logger.LogInformation($"Version {osVersion} not found. Please download.");
            }

            var osFiles = Directory.GetFiles(osFilePath);
            var files = Directory.GetFiles(applicationPath);

            using (var archive = ZipFile.Open(zipFile, ZipArchiveMode.Create))
            {
                foreach (var fPath in files)
                {
                    archive.CreateEntryFromFile(fPath, Path.Combine("app", Path.GetFileName(fPath)));
                }

                foreach (var fPath in osFiles)
                {
                    archive.CreateEntryFromFile(fPath, Path.Combine("os", Path.GetFileName(fPath)));
                }
            }

            _logger.LogInformation($"{zipFile} created");

            return zipFile;
        }

        public async Task UploadPackage(string mpakPack)
        {
            var identityManager = new IdentityManager(_logger);

            HttpClient httpClient = new HttpClient();
            var token = await identityManager.GetAccessToken();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var json = await httpClient.GetStringAsync($"{Constants.CLOUD_HOST_URI}/api/users/me");
            var user = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            var uploadFilename = $"{Path.GetFileName(mpakPack)}";

            _logger.LogInformation($"Uploading {uploadFilename}...");

            using (var multipartFormContent = new MultipartFormDataContent())
            {
                //Load the file and set the file's Content-Type header
                var fileStreamContent = new StreamContent(File.OpenRead(mpakPack));
                fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                //Add the file
                multipartFormContent.Add(fileStreamContent, name: "file", fileName: uploadFilename);

                //Send it
                var response = await httpClient.PostAsync($"{Constants.CLOUD_HOST_URI}/api/orgs/{user["orgId"]}/packages", multipartFormContent);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Upload complete");
                    _logger.LogInformation(await response.Content.ReadAsStringAsync());
                }
                else
                {
                    _logger.LogInformation($"There was an error while uploading the package: {response.StatusCode}");
                }
            }
        }

        public async Task PublishPackage(string packageId)
        {
            var identityManager = new IdentityManager(_logger);

            HttpClient httpClient = new HttpClient();
            var token = await identityManager.GetAccessToken();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var json = await httpClient.GetStringAsync($"{Constants.CLOUD_HOST_URI}/api/users/me");
            var user = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

            var response = await httpClient.PostAsync($"{Constants.CLOUD_HOST_URI}/api/orgs/{user["orgId"]}/packages/{packageId}/publish", null);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Publish complete");
            }
            else
            {
                _logger.LogInformation($"There was an error while publishing the package: {response.StatusCode}");
            }
        }

        public async Task ListPackages()
        {
            var identityManager = new IdentityManager(_logger);

            HttpClient httpClient = new HttpClient();
            var token = await identityManager.GetAccessToken();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var json = await httpClient.GetStringAsync($"{Constants.CLOUD_HOST_URI}/api/users/me");
            var user = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

            var packagesJson = await httpClient.GetStringAsync($"{Constants.CLOUD_HOST_URI}/api/orgs/{user["orgId"]}/packages");
            var packages = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(packagesJson);

            foreach (var item in packages)
            {
                _logger.LogInformation($"{item["id"]} {item["name"]}");
            }
        }
    }
}
