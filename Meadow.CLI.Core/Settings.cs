using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Meadow.CLI
{
    public static class SettingsManager
    {
        readonly static string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WildernessLabs", "clisettings.json");

        public static string GetSetting(Setting setting)
        {
            var settings = GetSettings();
            if(settings.TryGetValue(setting.ToString(), out var ret))
            {
                return ret;
            }
            return string.Empty;
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
            var json = JsonSerializer.Serialize(settings);
            File.WriteAllText(path, json);
        }

        private static Settings GetSettings()
        {
            Settings settings;

            if (File.Exists(path))
            {
                var json = File.ReadAllText(path); 
                settings = JsonSerializer.Deserialize<Settings>(json);
            }
            else
            {
                settings = new Settings();
            }
            return settings;
        }
    }

    public class Settings : Dictionary<string, string>
    {
    }

    public enum Setting
    {
        PORT
    }

}
