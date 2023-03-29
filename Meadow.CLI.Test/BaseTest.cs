using System;
using System.Threading;

namespace Meadow.CLI.Test
{
    public class BaseTest
    {
        protected static void WaitFor(int milliseconds)
        {
            var pause = new ManualResetEvent(false);
            pause.WaitOne(milliseconds);
        }

        protected static void WaitFor(TimeSpan timeSpan, Func<bool> func, int intervalInMS = 10)
        {
            var pause = new ManualResetEvent(false);
            TimeSpan total = timeSpan;
            TimeSpan interval = TimeSpan.FromMilliseconds(intervalInMS);
            while (total.TotalMilliseconds > 0)
            {
                pause.WaitOne(interval);
                total = total.Subtract(interval);
                if (func())
                {
                    break;
                }
            }
        }

        protected static bool WaitForDebuggerToStart(string logcatFilePath, int timeout = 120)
        {
            bool result = false; /* TODO = MonitorAdbLogcat((line) => {
                return line.IndexOf("Trying to initialize the debugger with options:", StringComparison.OrdinalIgnoreCase) > 0;
            }, logcatFilePath, timeout); */
            return result;
        }
    }
}
