using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using Meadow.Cli;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("config", Description = "Get the device info")]
public class ConfigCommand : ICommand
{
    private readonly ISettingsManager _settingsManager;
    private readonly ILogger<DeviceInfoCommand>? _logger;

    [CommandOption("list", IsRequired = false)]
    public bool List { get; set; }

    [CommandParameter(0, Name = "Settings", IsRequired = false)]
    public string[] Settings { get; set; }

    public ConfigCommand(ISettingsManager settingsManager, ILoggerFactory? loggerFactory)
    {
        _logger = loggerFactory?.CreateLogger<DeviceInfoCommand>();
        _settingsManager = settingsManager;
    }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (List)
        {
            _logger?.LogInformation($"Current CLI configuration");

            // display all current config
            var settings = _settingsManager.GetPublicSettings();
            if (settings.Count == 0)
            {
                _logger?.LogInformation($"  <no settings found>");
            }
            else
            {
                foreach (var kvp in _settingsManager.GetPublicSettings())
                {
                    _logger?.LogInformation($"  {kvp.Key} = {kvp.Value}");
                }
            }
        }
        else
        {
            switch (Settings.Length)
            {
                case 0:
                    // not valid
                    throw new CommandException($"No setting provided");
                case 1:
                    // erase a setting
                    _logger?.LogInformation($"Deleting Setting {Settings[0]}");
                    _settingsManager.DeleteSetting(Settings[0]);
                    break;
                case 2:
                    // set a setting
                    _logger?.LogInformation($"Setting {Settings[0]}={Settings[1]}");
                    _settingsManager.SaveSetting(Settings[0], Settings[1]);
                    break;
                default:
                    // not valid;
                    throw new CommandException($"Too many parameters provided");
            }
        }
    }
}
