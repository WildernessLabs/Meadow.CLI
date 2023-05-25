using System;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.CLI.Core.Progress
{
    public class ConsoleSpinnerProgress : BaseProgress
    {
        private int counter = 0;
        private char[] sequence = { '|', '/', '-', '\\' };

        public ConsoleSpinnerProgress(int delay = 100, CancellationToken cancellationToken = default)
            : base(delay, cancellationToken)
        {

        }

        public override async Task Report(int value = 0)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                counter++;
                Console.Write(sequence[counter % 4]);
                Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                try
                {
                    await Task.Delay(delay, cancellationToken); // Not propogating the token intentionally.
                }
                catch (TaskCanceledException)
                {
                    // We just each it for now
                }
            }
        }
    }
}