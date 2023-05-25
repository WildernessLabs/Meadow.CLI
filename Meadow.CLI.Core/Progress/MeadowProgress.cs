using System;

namespace Meadow.CLI.Core.Progress
{
	public class MeadowProgress
	{
        public event EventHandler<ProgressEventArgs>? ProgressChanged;

        // Method to report progress with a specified value and task name
        public void Report(int progressValue, string taskName)
        {
            var progressArgs = new ProgressEventArgs(progressValue, taskName);
            ProgressChanged?.Invoke(this, progressArgs);
        }
    }
}