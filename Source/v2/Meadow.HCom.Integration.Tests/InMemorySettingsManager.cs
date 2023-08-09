using Meadow.Cli;

namespace Meadow.HCom.Integration.Tests
{
    public class InMemorySettingsManager : ISettingsManager
    {
        private Dictionary<string, string> _publicSettings = new();

        public void DeleteSetting(string setting)
        {
            lock (_publicSettings)
            {
                if (_publicSettings.ContainsKey(setting))
                {
                    _publicSettings.Remove(setting);
                }
            }
        }

        public string? GetAppSetting(string name)
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, string> GetPublicSettings()
        {
            return _publicSettings;
        }

        public string? GetSetting(string setting)
        {
            lock (_publicSettings)
            {
                if (_publicSettings.TryGetValue(setting, out var value))
                {
                    return value;
                }
            }

            return null;
        }

        public void SaveSetting(string setting, string value)
        {
            lock (_publicSettings)
            {
                if (_publicSettings.ContainsKey(setting))
                {
                    _publicSettings[setting] = value;
                }
                else
                {
                    _publicSettings.Add(setting, value);
                }
            }
        }
    }
}