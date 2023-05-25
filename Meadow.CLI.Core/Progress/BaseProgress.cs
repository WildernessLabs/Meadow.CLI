using System;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.CLI.Core.Progress
{
    public abstract class BaseProgress : IMeadowProgress
    {
        protected int delay;
        protected CancellationToken cancellationToken;

        public BaseProgress(int delay = 100, CancellationToken cancellationToken = default)
        {
            this.delay = delay;
            this.cancellationToken = cancellationToken;
        }

        public abstract Task Report(int value);
    }
}