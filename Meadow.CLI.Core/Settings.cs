using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Meadow.CLI.Core
{
    public static class SettingsManager
    {
        private static readonly string Path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WildernessLabs", "clisettings.json");

        public static string? GetSetting(Setting setting)
        {
            var settings = GetSettings();
            return settings.TryGetValue(setting.ToString(), out var ret) ? ret : null;
        }

        public static void SaveSetting(Setting setting, string value)
        {
            var settings = GetSettings();
            if (settings.ContainsKey(setting.ToString()))
            {
                settings[setting.ToString()] = value;
            }
            else
            {
                settings.Add(setting.ToString(), value);
            }

            FileInfo fi = new FileInfo(Path);
            if (!Directory.Exists(fi.Directory.FullName))
            {
                Directory.CreateDirectory(fi.Directory.FullName);
            }

            var json = JsonSerializer.Serialize(settings);
            File.WriteAllText(Path, json);
        }

        public static string GetAppSetting(string name)
        {
            if (ConfigurationManager.AppSettings.AllKeys.Contains(name))
            {
                return ConfigurationManager.AppSettings[name];
            }
            else
            {
                throw new ArgumentException($"{name} setting not found.");
            }
        }

        private static Settings GetSettings()
        {
            if (File.Exists(Path))
            {
                var json = File.ReadAllText(Path); 
                var settings = JsonSerializer.Deserialize<Settings>(json);
                return settings ?? new Settings();
            }
            else
            {
                return new Settings();
            }
        }
    }

    public class Settings : Dictionary<string, string>
    {
    }

    public enum Setting
    {
        PORT,
        LastUpdateCheck,
        LatestVersion
    }

}
