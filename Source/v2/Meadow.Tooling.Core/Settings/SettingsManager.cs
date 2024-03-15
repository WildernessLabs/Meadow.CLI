using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Meadow.CLI;

public class SettingsManager : ISettingsManager
{
    private class Settings
    {
        public Dictionary<string, string> Public { get; set; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        public Dictionary<string, string> Private { get; set; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
    }

    public static class PublicSettings
    {
        public const string Route = "route";
        public const string LibUsb = "libusb";
    }

    private readonly string Path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WildernessLabs", "cli.settings");
    private const string PrivatePrefix = "private.";

    public Dictionary<string, string> GetPublicSettings()
    {
        var settings = GetSettings();
        return settings.Public;
    }

    public string? GetSetting(string setting)
    {
        var settings = GetSettings();
        if (settings.Public.TryGetValue(setting.ToString(), out var ret))
        {
            return ret;
        }
        else if (settings.Private.TryGetValue(setting.ToString(), out var pret))
        {
            return pret;
        }
        return null;
    }

    public void DeleteSetting(string setting)
    {
        var settings = GetSettings();
        Dictionary<string, string> target;

        if (setting.StartsWith(PrivatePrefix))
        {
            setting = setting.Substring(PrivatePrefix.Length);
            target = settings.Private;
        }
        else
        {
            target = settings.Public;
        }

        if (target.ContainsKey(setting.ToString()))
        {
            target.Remove(setting);

            var json = JsonSerializer.Serialize(settings);
            File.WriteAllText(Path, json);
        }
    }

    public void SaveSetting(string setting, string value)
    {
        var settings = GetSettings();
        Dictionary<string, string> target;

        if (setting.StartsWith(PrivatePrefix))
        {
            setting = setting.Substring(PrivatePrefix.Length);
            target = settings.Private;
        }
        else
        {
            target = settings.Public;
        }

        if (target.ContainsKey(setting))
        {
            target[setting] = value;
        }
        else
        {
            target.Add(setting, value);
        }

        var json = JsonSerializer.Serialize(settings);
        File.WriteAllText(Path, json);
    }

    public string? GetAppSetting(string name, string? defaultValue = null)
    {
        if (ConfigurationManager.AppSettings.AllKeys.Contains(name))
        {
            return ConfigurationManager.AppSettings[name];
        }

        return defaultValue;
    }

    private Settings GetSettings()
    {
        var fi = new FileInfo(Path);

        var directory = fi.Directory;
        if (directory != null)
        {
            var directoryFullName = directory?.FullName;
            if (!string.IsNullOrWhiteSpace(directoryFullName) && !Directory.Exists(directoryFullName))
            {
                Directory.CreateDirectory(directoryFullName);
            }
        }

        if (File.Exists(Path))
        {
            var json = File.ReadAllText(Path);
            return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
        }

        return new Settings();
    }
}