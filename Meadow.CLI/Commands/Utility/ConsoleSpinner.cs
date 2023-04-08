using System;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.CLI.Commands.Utility
{
	public class ConsoleSpinner
	{
        private int counter = 0;
        private char[] sequence = { '|', '/', '-', '\\' };

        public async Task Turn(int delay = 100, CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                counter++;
                Console.Write(sequence[counter % 4]);
                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                await Task.Delay(delay, CancellationToken.None); // Not propogating the token intentionally.
            }
        }
    }
}