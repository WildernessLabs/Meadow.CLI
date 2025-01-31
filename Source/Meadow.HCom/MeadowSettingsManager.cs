using System.Collections;
using YamlDotNet.Serialization;

namespace Meadow.Hcom;

public interface IMeadowSettingsManager
{
    Task WriteWiFiSettings(string ssid, string password);

    Task<Dictionary<string, object>> ReadDeviceSettings();
    Task WriteDeviceSetting(string key, string value);
    Task WriteDeviceSetting(IEnumerable<KeyValuePair<string, string>> values);

    Task<Dictionary<string, object>> ReadAppSettings();
    Task WriteAppSetting(string key, string value);
    Task WriteAppSetting(IEnumerable<KeyValuePair<string, string>> values);
}

public class MeadowSettingsManager : IMeadowSettingsManager
{
    private IMeadowDevice _device;
    private ISerializer _serializer;
    private IDeserializer _deserializer;

    public MeadowSettingsManager(IMeadowDevice device)
    {
        _device = device;
        _serializer = new SerializerBuilder().Build();
        _deserializer = new DeserializerBuilder()
            .Build();
    }

    public Task WriteAppSetting(string key, string value)
    {
        return WriteAppSetting(new Dictionary<string, string>
        {
            { key, value }
        });
    }

    public async Task WriteAppSetting(IEnumerable<KeyValuePair<string, string>> values)
    {
        var current = await ReadDeviceSettings();

        await WriteDeviceSettingFile(current, values, "app.config.yaml");
    }

    public Task WriteDeviceSetting(string key, string value)
    {
        return WriteDeviceSetting(new Dictionary<string, string>
        {
            { key, value }
        });
    }

    public async Task WriteDeviceSetting(IEnumerable<KeyValuePair<string, string>> values)
    {
        var current = await ReadDeviceSettings();

        await WriteDeviceSettingFile(current, values, "meadow.config.yaml");
    }

    private async Task WriteDeviceSettingFile(Dictionary<string, object> current, IEnumerable<KeyValuePair<string, string>> values, string settingFile)
    {
        foreach (var kvp in values)
        {
            AddValueToDictionary(current, kvp.Key, kvp.Value);
        }

        var fileName = Path.GetTempFileName();

        try
        {
            var yaml = _serializer.Serialize(current);
            File.WriteAllText(fileName, yaml);
            await _device.WriteFile(fileName, settingFile);
        }
        finally
        {
            File.Delete(fileName);
        }
    }

    private void AddValueToDictionary(IDictionary dict, string key, string value)
    {
        var levels = key.Split(':');
        var current = dict;

        for (int i = 0; i < levels.Length - 1; i++)
        {
            if (!current.Contains(levels[i]))
            {
                current[levels[i]] = new Dictionary<object, object>();
            }
            current = (IDictionary)current[levels[i]];
        }

        current[levels[levels.Length - 1]] = value;
    }

    // Usage:
    //Add(config, "Logging:LogLevel:Default", "Debug");
    public Task<Dictionary<string, object>> ReadAppSettings()
    {
        return ReadDeviceYamlFile("app.config.yaml");
    }

    public Task<Dictionary<string, object>> ReadDeviceSettings()
    {
        return ReadDeviceYamlFile("meadow.config.yaml");
    }

    private async Task<Dictionary<string, object>> ReadDeviceYamlFile(string fileName)
    {
        var yaml = await _device.ReadFileString(fileName);
        if (yaml == null)
        {
            return new Dictionary<string, object>();
        }

        // yamldotnet doesn't handle comments well, so scrub manually
        var lines = yaml.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
           .Where(l => !l.TrimStart().StartsWith("#"));
        var cleanYaml = string.Join("\n", lines);

        var settings = _deserializer.Deserialize<Dictionary<string, object>>(cleanYaml);
        return settings;
    }

    public async Task WriteWiFiSettings(string ssid, string password)
    {
        var yaml = _serializer.Serialize(
            new
            {
                Credentials = new
                {
                    Ssid = ssid,
                    Password = password
                }
            });

        var fileName = Path.GetTempFileName();

        try
        {
            File.WriteAllText(fileName, yaml);
            await _device.WriteFile(fileName, "wifi.config.yaml");
        }
        finally
        {
            File.Delete(fileName);
        }
    }
}
