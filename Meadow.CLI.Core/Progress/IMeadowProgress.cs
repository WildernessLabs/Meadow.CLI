using System;
using System.Threading.Tasks;

namespace Meadow.CLI.Core.Progress
{
    public interface IMeadowProgress
    {
        Task Report(int value);
    }
}