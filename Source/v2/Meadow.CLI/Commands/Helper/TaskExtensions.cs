using CliFx.Infrastructure;

namespace Meadow.CLI
{
    public static class TaskExtensions
    {
        public static async Task WithSpinner(this Task task, IConsole console, int delay = 100, CancellationToken cancellationToken = default)
        {
            var spinnerCancellationTokenSource = new CancellationTokenSource();
            var consoleSpinner = new ConsoleSpinner(console);

            var consoleSpinnerTask = consoleSpinner.Turn(delay, spinnerCancellationTokenSource.Token);

            try
            {
                await task;
            }
            finally
            {
                // Cancel the spinner when the original task completes
                spinnerCancellationTokenSource.Cancel();

                // Let's wait for the spinner to finish
                await consoleSpinnerTask;
            }
        }

        public static async Task<T> WithSpinner<T>(this Task<T> task, IConsole console, int delay = 100, CancellationToken cancellationToken = default)
        {
            // Get our spinner read
            var spinnerCancellationTokenSource = new CancellationTokenSource();
            var consoleSpinner = new ConsoleSpinner(console);

            Task consoleSpinnerTask = consoleSpinner.Turn(delay, spinnerCancellationTokenSource.Token);

            try
            {
                return await task;
            }
            finally
            {
                // Cancel the spinner when the original task completes
                spinnerCancellationTokenSource.Cancel();

                // Let's wait for the spinner to finish
                await consoleSpinnerTask;
            }
        }
    }
}