using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace Meadow.Hcom;

public static partial class JsonSerializerExtensions
{
    public static T? DeserializeAnonymousType<T>(string json, T anonymousTypeObject, JsonSerializerOptions? options = default)
        => JsonSerializer.Deserialize<T>(json, options);

    public static ValueTask<TValue?> DeserializeAnonymousTypeAsync<TValue>(Stream stream, TValue anonymousTypeObject, JsonSerializerOptions? options = default, CancellationToken cancellationToken = default)
        => JsonSerializer.DeserializeAsync<TValue>(stream, options, cancellationToken); // Method to deserialize from a stream added for completeness
}

public static class FirmwareManager
{
    public static async Task<string?> GetRemoteFirmwareInfo(string versionNumber, ILogger logger)
    {
        var manager = new DownloadManager(logger);

        return await manager.DownloadMeadowOSVersionFile(versionNumber);
    }

    public static async Task GetRemoteFirmware(string versionNumber, ILogger logger)
    {
        var manager = new DownloadManager(logger);

        await manager.DownloadOsBinaries(versionNumber, true);
    }

    public static async Task<string> GetCloudLatestFirmwareVersion()
    {
        var request = (HttpWebRequest)WebRequest.Create($"{DownloadManager.VersionCheckUrlRoot}latest.json");
        using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
        using (Stream stream = response.GetResponseStream())
        using (StreamReader reader = new StreamReader(stream))
        {
            var json = await reader.ReadToEndAsync();

            if (json == null) return string.Empty;

            return JsonSerializerExtensions.DeserializeAnonymousType(json, new { version = string.Empty }).version;
        }
    }

    public static string GetLocalLatestFirmwareVersion()
    {
        var di = new DirectoryInfo(DownloadManager.FirmwareDownloadsFilePathRoot);
        var latest = string.Empty;
        var latestFile = di.GetFiles("latest.txt").FirstOrDefault();
        if (latestFile != null)
        {
            latest = File.ReadAllText(latestFile.FullName).Trim();
        }
        return latest;
    }

    public static FirmwareInfo[] GetAllLocalFirmwareBuilds()
    {
        var list = new List<FirmwareInfo>();

        var di = new DirectoryInfo(DownloadManager.FirmwareDownloadsFilePathRoot);

        var latest = GetLocalLatestFirmwareVersion();

        var options = new JsonSerializerOptions();
        options.Converters.Add(new BuildDateConverter());

        FirmwareInfo? ParseInfo(string version, string json)
        {
            var fi = JsonSerializer.Deserialize<FirmwareInfo>(json, options);
            if (fi == null) return null;
            fi.Version = version;
            fi.IsLatest = version == latest;
            return fi;
        }

        foreach (var dir in di.EnumerateDirectories())
        {
            var info = dir.GetFiles("build-info.json").FirstOrDefault();
            if (info == null) continue;
            var json = File.ReadAllText(info.FullName);
            try
            {
                var fi = ParseInfo(dir.Name, json);
                if (fi != null)
                {
                    list.Add(fi);
                }
            }
            catch (JsonException)
            {
                // work around for Issue #229 (bad json)
                var index = json.IndexOf(']');
                if (index != -1 && json[index + 1] == ',')
                {
                    var fix = $"{json.Substring(0, index + 1)}{json.Substring(index + 2)}";
                    try
                    {
                        var fi = ParseInfo(dir.Name, fix);
                        if (fi != null)
                        {
                            list.Add(fi);
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                continue;
            }
        }
        return list.ToArray();
    }

    public static FirmwareUpdater GetFirmwareUpdater(IMeadowConnection connection)
    {
        return new FirmwareUpdater(connection);
    }

    public static async Task PushApplicationToDevice(IMeadowConnection connection, DirectoryInfo appFolder, ILogger? logger = null)
    {
        try
        {
            if (connection == null) throw new ArgumentNullException("connection");
            if (connection.Device == null) throw new ArgumentNullException("connection.Device");


            var info = await connection.Device.GetDeviceInfo();

            await connection.Device.RuntimeDisable();
            // the device will disconnect and reconnect here

            //            await connection.Device.DeployApp(Path.Combine(appFolder.FullName, "App.dll"), osVersion);

            await connection.Device.RuntimeEnable();
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error flashing OS to Meadow");
        }
    }
}
