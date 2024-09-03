using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("config source", Description = "Sets the root folder for Meadow source directories")]
public class ConfigSourceCommand : BaseSettingsCommand<ConfigCommand>
{
    [CommandParameter(0, Name = "Root", IsRequired = false)]
    public string? Root { get; init; }

    public ConfigSourceCommand(ISettingsManager settingsManager, ILoggerFactory loggerFactory)
        : base(settingsManager, loggerFactory)
    { }


    protected override ValueTask ExecuteCommand()
    {
        var root = Root;

        // if Root is null, as the user if they want to use the current folder
        if (string.IsNullOrWhiteSpace(Root))
        {
            System.Console.Write("No root folder provided.  You you want to use the current directory? (y/n) ");
            if (System.Console.ReadLine()?.Trim() != "y")
            {
                System.Console.WriteLine("cancelled");
                return ValueTask.CompletedTask;
            }

            root = Environment.CurrentDirectory;
        }

        root!.Trim('\'').Trim('"'); ;
        Logger?.LogInformation($"{Environment.NewLine}Setting source={root}");
        SettingsManager.SaveSetting("source", root!);

        return ValueTask.CompletedTask;
    }
}
