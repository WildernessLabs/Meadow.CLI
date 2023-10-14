using Meadow.Cloud.Identity;
using System.IO.Compression;
using System.IO.Hashing;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Meadow.Cloud;

public class PackageService : CloudServiceBase
{
    private string _info_json = "info.json";

    public PackageService(IdentityManager identityManager) : base(identityManager)
    {
    }

    public async Task<Package> UploadPackage(
        string mpakPath,
        string orgId,
        string description,
        string host,
        CancellationToken? cancellationToken = null)
    {
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

        var crcFileHash = await CalculateCrc32FileHash(mpakPath);

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
            if (packageInfo != null)
            {
                result = packageInfo.OsVersion;
            }
            File.Delete(tempInfoJson);
        }

        return result!;
    }

    public async Task PublishPackage(
        string packageId,
        string collectionId,
        string metadata,
        string host,
        CancellationToken? cancellationToken = null)
    {
        var httpClient = await GetAuthenticatedHttpClient(cancellationToken);

        var payload = new { metadata, collectionId };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response =
            await httpClient.PostAsync($"{host}/api/packages/{packageId}/publish", content, cancellationToken ?? CancellationToken.None);

        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new MeadowCloudException(message);
        }
    }

    public async Task<List<Package>> GetOrgPackages(string orgId, string host, CancellationToken? cancellationToken = null)
    {
        var httpClient = await GetAuthenticatedHttpClient(cancellationToken);

        var result = await httpClient.GetStringAsync($"{host}/api/orgs/{orgId}/packages");

        return JsonSerializer.Deserialize<List<Package>>(result) ?? new List<Package>();
    }

    private async Task<string> CalculateCrc32FileHash(string filePath)
    {
        var crc32 = new Crc32();

        using (var fs = File.OpenRead(filePath))
        {
            await crc32.AppendAsync(fs);
        }

        var checkSum = crc32.GetCurrentHash();
        Array.Reverse(checkSum); // make big endian
        return BitConverter.ToString(checkSum).Replace("-", "").ToLower();
    }
}
