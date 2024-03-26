using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.CLI.Core
{
    public static partial class JsonSerializerExtensions
    {
        public static T? DeserializeAnonymousType<T>(string json, T anonymousTypeObject, JsonSerializerOptions? options = default)
            => JsonSerializer.Deserialize<T>(json, options);

        public static ValueTask<TValue?> DeserializeAnonymousTypeAsync<TValue>(Stream stream, TValue anonymousTypeObject, JsonSerializerOptions? options = default, CancellationToken cancellationToken = default)
            => JsonSerializer.DeserializeAsync<TValue>(stream, options, cancellationToken); // Method to deserialize from a stream added for completeness
    }

    public static class FirmwareManager
    {
        public static async Task<string?> GetRemoteFirmwareInfo(string versionNumber, ILogger logger, CancellationToken cancellationToken)
        {
            var manager = new DownloadManager(logger);

            return await manager.DownloadMeadowOSVersionFile(versionNumber, cancellationToken);
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
                catch (JsonException ex)
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

        public static FirmwareUpdater GetFirmwareUpdater(MeadowConnectionManager connectionManager)
        {
            return new FirmwareUpdater(connectionManager);
        }



        public static async Task PushApplicationToDevice(IMeadowConnection connection, DirectoryInfo appFolder, ILogger? logger = null)
        {
            try
            {
                if (connection == null) throw new ArgumentNullException("connection");
                if (connection.Device == null) throw new ArgumentNullException("connection.Device");
                if (!connection.IsConnected)
                {
                    if (!await connection.WaitForConnection(TimeSpan.FromSeconds(5)))
                    {
                        throw new Exception("No device connected");
                    }
                }

                connection.AutoReconnect = false;

                if (connection.Device.DeviceInfo == null)
                {
                    await connection.Device.GetDeviceInfo(TimeSpan.FromSeconds(5));
                }

                var osVersion = connection.Device.DeviceInfo.MeadowOsVersion;

                await connection.Device.MonoDisable();
                // the device will disconnect and reconnect here

                // can't check "is connected" immediately, as it take a few hundred ms to disable mono and restart
                await Task.Delay(1000);

                // wait for reconnect
                await connection.WaitForConnection(TimeSpan.FromSeconds(15));

                await connection.Device.DeployApp(Path.Combine(appFolder.FullName, "App.dll"), osVersion);

                await connection.Device.MonoEnable();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error flashing OS to Meadow");
            }
            finally
            {
                connection.AutoReconnect = true;
            }
        }
    }
}
