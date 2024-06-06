using System;
using CliFx;
using CliFx.Exceptions;
using System.Diagnostics;

namespace Meadow.Updater;

public class Program
{
	public static async Task<int> Main(string[] args)
	{
        int returnCode;

        try
        {
            returnCode = await new CliApplicationBuilder()
                .AddCommandsFromThisAssembly()
                //.UseTypeActivator(serviceProvider.GetService!)
                .SetExecutableName("Meadow.Updater")
                .Build()
                .RunAsync();
        }
        catch (CommandException ce)
        {
            returnCode = ce.ExitCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Operation failed: {ex.Message}");
            returnCode = 1;
        }

		return returnCode;
	}
}