using System.Threading;
using System.Threading.Tasks;
using CliFx;
using CliFx.Attributes;
using CliFx.Infrastructure;

namespace Meadow.CommandLine.Commands
{
    public abstract class MeadowSerialCommand : ICommand
    {
        [CommandOption("port", 's', Description = "Meadow COM port", IsRequired = true)]
        public string SerialPortName { get; init; }

        [CommandOption("listen", 'k', Description = "Keep port open to listen for output")]
        public bool Listen {get; init;}

        //private protected CancellationToken CancellationToken { get; init; }

        public abstract ValueTask ExecuteAsync(IConsole console);
    }
}
