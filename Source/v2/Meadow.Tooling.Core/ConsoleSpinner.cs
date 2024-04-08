using System;
using System.Threading;
using System.Threading.Tasks;
using CliFx.Infrastructure;

namespace Meadow.CLI
{
    public static class ConsoleSpinner
    {
        private static readonly char[] sequence = { '|', '/', '-', '\\' };

        private static CancellationToken? token;

        public static void Spin(IConsole? console, int udpateInterval_ms = 100, CancellationToken cancellationToken = default)
        {
            if (console == null)
            {
                throw new ArgumentNullException(nameof(console));
            }

            if (token != null)
            {
                throw new InvalidOperationException("A spinner is already running");
            }
            token = cancellationToken;

            _ = Task.Run(async () =>
            {
                int index = 0;

                while (cancellationToken.IsCancellationRequested == false)
                {
                    index++;
                    console?.Output.WriteAsync($"{sequence[index % 4]}         \r");
                    await Task.Delay(udpateInterval_ms, CancellationToken.None);
                }
            }, cancellationToken);
        }
    }
}