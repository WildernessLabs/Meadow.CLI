using Newtonsoft.Json.Linq;
using Serilog;
using System.Reflection;

namespace Meadow.CLI;

public static class VersionChecker
{
    private static readonly string PackageId = "WildernessLabs.Meadow.CLI";
    private static readonly string NugetApiUrl = $"https://api.nuget.org/v3-flatcontainer/{PackageId.ToLower()}/index.json";
    private static readonly TimeSpan CheckFrequency = TimeSpan.FromDays(1);
    private static readonly string LastCheckKey = "last_check_time";

    public static async Task CheckForUpdates(ILogger? logger, ISettingsManager settingsManager)
    {
        if (DateTime.UtcNow - GetLastCheckTime(settingsManager) < CheckFrequency)
        {
            return;
        }

        string currentVersion = GetCurrentVersion();

        using var httpClient = new HttpClient();

        try
        {
            var response = await httpClient.GetStringAsync(NugetApiUrl);

            var json = JObject.Parse(response);

            // versions aren't guaranteed to be ordered (esp. across pre-release tags), so pick the max
            var latestPublishedVersion = (json["versions"] as JArray)?
                .Select(v => v.ToString())
                .OrderBy(v => v, SemVerComparer.Instance)
                .LastOrDefault();

            if (latestPublishedVersion != null &&
                SemVerComparer.Instance.Compare(latestPublishedVersion, currentVersion) > 0)
            {
                logger?.Information($"\r\nMeadow.CLI {latestPublishedVersion} is available - run 'dotnet tool update {PackageId} -g' to update");
            }

            SetLastCheckTime(settingsManager, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            logger?.Debug($"Error checking for updates: {ex.Message}");
        }
    }

    private static string GetCurrentVersion()
    {
        var assembly = Assembly.GetEntryAssembly();

        // prefer the informational version - it carries the full SemVer (incl. pre-release tag);
        // the file version is numeric-only (e.g. 3.0.0.0) and can't be compared to NuGet's tags
        var informational = assembly?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informational))
        {
            // strip build metadata (e.g. +<commit-sha>) if present
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational.Substring(0, plus) : informational;
        }

        return assembly?.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "0.0.0";
    }

    private static DateTime GetLastCheckTime(ISettingsManager settingsManager)
    {
        var lastCheckString = settingsManager.GetSetting(LastCheckKey);
        if (DateTime.TryParse(lastCheckString, out DateTime lastCheck))
        {
            return lastCheck;
        }
        return DateTime.MinValue;
    }

    private static void SetLastCheckTime(ISettingsManager settingsManager, DateTime dateTime)
    {
        settingsManager.SaveSetting(LastCheckKey, dateTime.ToString("o"));
    }

    // Minimal SemVer 2.0 precedence comparison - System.Version can't parse pre-release tags
    // (e.g. "3.0.0-beta3"), so it can't be used to compare against NuGet's published versions.
    private sealed class SemVerComparer : IComparer<string>
    {
        public static readonly SemVerComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            var (xCore, xPre) = Parse(x);
            var (yCore, yPre) = Parse(y);

            for (var i = 0; i < 3; i++)
            {
                var c = xCore[i].CompareTo(yCore[i]);
                if (c != 0) return c;
            }

            // a version with no pre-release tag outranks one that has it
            if (xPre.Length == 0 && yPre.Length == 0) return 0;
            if (xPre.Length == 0) return 1;
            if (yPre.Length == 0) return -1;

            var len = Math.Min(xPre.Length, yPre.Length);
            for (var i = 0; i < len; i++)
            {
                var c = ComparePreReleaseIdentifier(xPre[i], yPre[i]);
                if (c != 0) return c;
            }
            return xPre.Length.CompareTo(yPre.Length);
        }

        private static (int[] core, string[] pre) Parse(string? version)
        {
            var core = new[] { 0, 0, 0 };
            if (string.IsNullOrWhiteSpace(version))
            {
                return (core, Array.Empty<string>());
            }

            // drop build metadata
            var plus = version.IndexOf('+');
            if (plus >= 0) version = version.Substring(0, plus);

            var pre = Array.Empty<string>();
            var dash = version.IndexOf('-');
            if (dash >= 0)
            {
                pre = version.Substring(dash + 1).Split('.');
                version = version.Substring(0, dash);
            }

            var parts = version.Split('.');
            for (var i = 0; i < 3 && i < parts.Length; i++)
            {
                int.TryParse(parts[i], out core[i]);
            }
            return (core, pre);
        }

        private static int ComparePreReleaseIdentifier(string a, string b)
        {
            var aNum = int.TryParse(a, out var ai);
            var bNum = int.TryParse(b, out var bi);

            if (aNum && bNum) return ai.CompareTo(bi);
            // numeric identifiers have lower precedence than alphanumeric ones
            if (aNum) return -1;
            if (bNum) return 1;
            return string.CompareOrdinal(a, b);
        }
    }
}