using System;
using System.Diagnostics;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;

namespace Meadow.Updater;

[Command("update", Description = Strings.Update.Description)]
public class UpdateToolCommand : ICommand
{
    const int DEFAULT_DELAY_SECONDS = 2;

    [CommandOption("tool", 't', IsRequired = false)]
    public string? Tool { get; set; }

    [CommandOption("version", 'v', IsRequired = false)]
    public string? Version { get; set; }

    public async ValueTask ExecuteAsync(IConsole console)
    {
        if (string.IsNullOrWhiteSpace(Tool))
        {
            console.Error.WriteLine(Strings.Update.NoToolSpecified);
            return;
        }

        // Wait for the specified delay to ensure the process that called this has exited
        await Task.Delay(TimeSpan.FromSeconds(DEFAULT_DELAY_SECONDS));

        // Uninstall the previous version in case we're updating to a previous Version 
        await RunCommand("dotnet", $"tool uninstall {Tool} --global", console);

        if (!string.IsNullOrWhiteSpace(Version))
        {
            Tool += $" --version {Version}";
        }

        // Now do the update
        await RunCommand("dotnet", $"tool update {Tool} --global", console);
    }

    internal static async Task RunCommand(string command, string arguments, IConsole console)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(processStartInfo))
        {
            while (!process.StandardOutput.EndOfStream)
            {
                var line = await process.StandardOutput.ReadLineAsync();
                console.Output.WriteLine(line ?? string.Empty);
            }

            while (!process.StandardError.EndOfStream)
            {
                var line = await process.StandardError.ReadLineAsync();
                console.Error.WriteLine(line ?? string.Empty);
            }

            await process.WaitForExitAsync();
        }
    }
}