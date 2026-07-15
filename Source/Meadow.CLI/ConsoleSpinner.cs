using CliFx.Infrastructure;

namespace Meadow.CLI
{
    /// <summary>
    /// A small console "working" animation. Each spinner is an independent, disposable
    /// instance with no shared state, so multiple spins in a single process (or overlapping
    /// spins) can never collide. The animation is purely cosmetic and will never throw or
    /// otherwise interfere with the operation it decorates.
    ///
    /// Usage:
    ///   using (ConsoleSpinner.Start(console))
    ///   {
    ///       DoWork();
    ///   }               // Dispose stops the animation and clears the line
    /// </summary>
    public sealed class ConsoleSpinner : IDisposable
    {
        private static readonly char[] Sequence = { '|', '/', '-', '\\' };

        private readonly IConsole? _console;
        private readonly CancellationTokenSource _cts;
        private readonly Task _task;

        private ConsoleSpinner(IConsole? console, int updateInterval_ms, CancellationToken cancellationToken)
        {
            _console = console;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // No console -> nothing to animate. Keep a completed task so Dispose is a no-op.
            _task = console == null
                ? Task.CompletedTask
                : RunAsync(updateInterval_ms, _cts.Token);
        }

        /// <summary>
        /// Starts a spinner. Returns a disposable handle; dispose it (e.g. via <c>using</c>)
        /// to stop the animation and clear the line. A null console yields a no-op handle.
        /// </summary>
        public static ConsoleSpinner Start(IConsole? console, int updateInterval_ms = 100, CancellationToken cancellationToken = default)
        {
            return new ConsoleSpinner(console, updateInterval_ms, cancellationToken);
        }

        private async Task RunAsync(int updateInterval_ms, CancellationToken cancellationToken)
        {
            var index = 0;

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await _console!.Output.WriteAsync($"{Sequence[index++ % Sequence.Length]}         \r");
                    await Task.Delay(updateInterval_ms, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // expected on stop
            }
            catch
            {
                // The spinner is cosmetic; never let a console/animation error surface.
            }
            finally
            {
                // Erase the spinner glyph so it doesn't linger before the next output.
                try { await _console!.Output.WriteAsync("\r          \r"); } catch { /* ignore */ }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();

            // Wait (bounded) for the animation loop to finish clearing the line so its final
            // write can't land after later output. Cancelling the Task.Delay makes this return
            // almost immediately; the cap is just a safety net.
            try { _task.Wait(TimeSpan.FromMilliseconds(500)); }
            catch { /* ignore */ }

            _cts.Dispose();
        }
    }
}
