using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using Meadow.Cli;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("config", Description = "Read or modify the meadow CLI configuration")]
public class ConfigCommand : BaseSettingsCommand<ConfigCommand>
{
    [CommandOption("list", IsRequired = false)]
    public bool List { get; set; }

    [CommandParameter(0, Name = "Settings", IsRequired = false)]
    public string[] Settings { get; set; }

    public ConfigCommand(ISettingsManager settingsManager, ILoggerFactory? loggerFactory) : base(settingsManager, loggerFactory)
    {
        
    }

    protected override async ValueTask ExecuteCommand(IConsole console, CancellationToken cancellationToken)
    {
        if (List)
        {
            Logger?.LogInformation($"Current CLI configuration");

            // display all current config
            var settings = SettingsManager.GetPublicSettings();
            if (settings.Count == 0)
            {
                Logger?.LogInformation($"  <no settings found>");
            }
            else
            {
                foreach (var kvp in SettingsManager.GetPublicSettings())
                {
                    Logger?.LogInformation($"  {kvp.Key} = {kvp.Value}");
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
                    Logger?.LogInformation($"Deleting Setting {Settings[0]}");
                    SettingsManager.DeleteSetting(Settings[0]);
                    break;
                case 2:
                    // set a setting
                    Logger?.LogInformation($"Setting {Settings[0]}={Settings[1]}");
                    SettingsManager.SaveSetting(Settings[0], Settings[1]);
                    break;
                default:
                    // not valid;
                    throw new CommandException($"Too many parameters provided");
            }
        }
    }
}