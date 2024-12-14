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
            var latestPublishedVersion = $"{json["versions"].Last()}";

            if (latestPublishedVersion != null &&
                Version.TryParse(currentVersion, out Version? currentVersionParsed) &&
                Version.TryParse(latestPublishedVersion, out Version? latestVersionParsed) &&
                latestVersionParsed > currentVersionParsed)
            {

                logger?.Information($"\r\nMeadow.CLI {latestVersionParsed} is avaliable - run 'dotnet tool update {PackageId} -g' to update");
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
        return Assembly.GetEntryAssembly()?
                           .GetCustomAttribute<AssemblyFileVersionAttribute>()?
                           .Version ?? "2.0.0";
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
}