using CliFx;
using CliFx.Attributes;
using CliFx.Exceptions;
using CliFx.Infrastructure;
using System.Threading.Tasks;

namespace Meadow.CLI.Commands.Cloud
{
    [Command("cloud", Description = "Provides Meadow.Cloud service related commands.")]
    public class CloudCommand : ICommand
    {
        public ValueTask ExecuteAsync(IConsole console)
        {
            throw new CommandException("Please use one of the cloud subcommands.", showHelp: true);
        }
    }

    [Command("cloud command", Description = "Provides command & control related commands for devices.")]
    public class CloudCommandCommand : ICommand
    {
        public ValueTask ExecuteAsync(IConsole console)
        {
            throw new CommandException("Please use one of the cloud command subcommands.", showHelp: true);
        }
    }
}
