using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Meadow.CLI.Core
{
    public static class FirmwareManager
    {
        public static string GetLatestFirmwareVersion()
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

            var latest = GetLatestFirmwareVersion();

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
    }
}
