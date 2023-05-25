using System;
namespace Meadow.CLI.Core.Progress
{
    public class ProgressEventArgs : EventArgs
    {
        public int Value;
        public string Name;

        public ProgressEventArgs(int value, string name)
        {
            Value = value;
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }
    }
}