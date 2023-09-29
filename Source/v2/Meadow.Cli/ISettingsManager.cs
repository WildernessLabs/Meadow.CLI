namespace Meadow.Cli;

public interface ISettingsManager
{
    void DeleteSetting(string setting);
    string? GetAppSetting(string name, string? defaultValue = null);
    Dictionary<string, string> GetPublicSettings();
    string? GetSetting(string setting);
    void SaveSetting(string setting, string value);
}