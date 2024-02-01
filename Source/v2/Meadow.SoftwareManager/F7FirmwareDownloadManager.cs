using Meadow.Cloud.Client;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.Software;

internal class F7FirmwareDownloadManager
{
    //private const string VersionCheckUrlPath = "/api/v1/firmware/Meadow_Beta/";

    //private readonly HttpClient _client;
    private readonly IMeadowCloudClient _meadowCloudClient;

    public event EventHandler<long> DownloadProgress = default!;

    public F7FirmwareDownloadManager(IMeadowCloudClient meadowCloudClient)
    {
        _meadowCloudClient = meadowCloudClient;
    }

    public async Task<string> GetLatestAvailableVersion()
    {
        var contents = await GetReleaseMetadata();

        return contents?.Version ?? string.Empty;
    }

    public async Task<F7ReleaseMetadata?> GetReleaseMetadata(string? version = null, CancellationToken cancellationToken = default)
    {
        version = !string.IsNullOrWhiteSpace(version) ? version : "latest";
        var response = await _meadowCloudClient.Firmware.GetFirmwareVersionAsync("Meadow_Beta", version, cancellationToken);

        return new F7ReleaseMetadata()
        {
            Version = response.Result.Version,
            MinCLIVersion = response.Result.MinCLIVersion,
            DownloadURL = response.Result.DownloadUrl,
            NetworkDownloadURL = response.Result.NetworkDownloadUrl
        };
    }

    public void SetDefaultVersion(string destinationRoot, string version)
    {
        File.WriteAllText(Path.Combine(destinationRoot, "latest.txt"), version);
    }

    public async Task<bool> DownloadRelease(string destinationRoot, string version, bool overwrite = false)
    {
        var meta = await GetReleaseMetadata(version);
        if (meta == null) return false;

        CreateFolder(destinationRoot, false);

        string local_path;

        if (string.IsNullOrWhiteSpace(version))
        {
            local_path = Path.Combine(destinationRoot, meta.Version);
            version = meta.Version;
        }
        else
        {
            local_path = Path.Combine(destinationRoot, version);
        }

        if (CreateFolder(local_path, overwrite) == false)
        {
            throw new Exception($"Firmware version {version} already exists locally");
        }

        try
        {
            await DownloadAndExtractFile(new Uri(meta.DownloadURL), local_path);
        }
        catch
        {
            throw new Exception($"Unable to download OS files for {version}");
        }

        try
        {
            await DownloadAndExtractFile(new Uri(meta.NetworkDownloadURL), local_path);
        }
        catch
        {
            throw new Exception($"Unable to download Coprocessor files for {version}");
        }

        return true;
    }

    private async Task DownloadAndExtractFile(Uri uri, string target_path, CancellationToken cancellationToken = default)
    {
        var downloadFileName = await DownloadFile(uri, cancellationToken);

        ZipFile.ExtractToDirectory(
            downloadFileName,
            target_path);

        File.Delete(downloadFileName);
    }

    private bool CreateFolder(string path, bool eraseIfExists = true)
    {
        if (Directory.Exists(path))
        {
            if (eraseIfExists)
            {
                CleanPath(path);
            }
            else
            {
                return false;
            }
        }
        else
        {
            Directory.CreateDirectory(path);
        }
        return true;
    }

    private void CleanPath(string path)
    {
        var di = new DirectoryInfo(path);
        foreach (FileInfo file in di.GetFiles())
        {
            file.Delete();
        }
        foreach (DirectoryInfo dir in di.GetDirectories())
        {
            dir.Delete(true);
        }
    }

    private async Task<string> DownloadFile(Uri uri, CancellationToken cancellationToken = default)
    {
        using var response = await _meadowCloudClient.Firmware.GetFirmwareDownloadResponseAsync(uri.ToString(), cancellationToken);

        var downloadFileName = Path.GetTempFileName();

        using var stream = await response.Content.ReadAsStreamAsync();

        var contentLength = response.Content.Headers.ContentLength;

        using var downloadFileStream = new DownloadFileStream(stream);
        using var firmwareFile = File.OpenWrite(downloadFileName);

        downloadFileStream.DownloadProgress += (s, e) => { DownloadProgress?.Invoke(this, e); };

        await downloadFileStream.CopyToAsync(firmwareFile);

        return downloadFileName;
    }
}
