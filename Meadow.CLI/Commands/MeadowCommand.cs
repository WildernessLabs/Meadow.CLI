using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands
{
    public abstract class MeadowCommand : ICommand
    {
        private protected ILoggerFactory LoggerFactory;

        private protected MeadowCommand(ILoggerFactory loggerFactory)
        {
            LoggerFactory = loggerFactory;
        }

        [CommandOption('v', Description = "Log verbosity")]
        public string[] Verbosity { get; init; }

        public abstract ValueTask ExecuteAsync(IConsole console);
    }
}
