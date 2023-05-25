using System;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.CLI.Core.Progress
{
    public class ConsoleProgressBar : BaseProgress
    {
        private int totalSteps;

        public ConsoleProgressBar(int totalSteps = 20, CancellationToken cancellationToken = default)
            : base(100, cancellationToken)
        {
            this.totalSteps = totalSteps;
        }

        public override async Task Report(int currentValue = 0)
        {
            try
            {
                decimal progressPercentage = Math.Round((decimal)currentValue / totalSteps, 2);
                int progressBarWidth = Console.WindowWidth - 20;
                int completedBarWidth = (int)Math.Floor(progressPercentage * progressBarWidth);

                Console.Write($"Progress: [{new string('█', completedBarWidth)}{new string('.', progressBarWidth - completedBarWidth)}] {progressPercentage:P}");
                if (currentValue >= totalSteps)
                {
                    Console.WriteLine();
                }


                await Task.Delay(delay, cancellationToken); // Not propogating the token intentionally.
            }
            catch (TaskCanceledException)
            {
                // We just each it for now
            }
        }
    }
}