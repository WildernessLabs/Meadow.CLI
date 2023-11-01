
using CliFx.Infrastructure;

namespace Meadow.CLI
{
    public class ConsoleSpinner
    {
        private int counter = 0;
        private char[] sequence = { '|', '/', '-', '\\' };
        private IConsole console;

        public ConsoleSpinner(IConsole console)
        {
            this.console = console;
        }

        public async Task Turn(int delay = 100, CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                counter++;
                console?.Output.WriteAsync($"{sequence[counter % 4]}         \r");
                await Task.Delay(delay, CancellationToken.None); // Not propogating the token intentionally.
            }
        }
    }
}