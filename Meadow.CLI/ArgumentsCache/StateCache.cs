using System.IO;

namespace MeadowCLI
{
    public static class StateCache
    {
        private const string StateFileName = "_clicache";

        public static State Load()
        {
            var state = new State();

            var fi = new FileInfo(StateFileName);
            if (fi.Exists)
            {
                foreach (var line in File.ReadLines(StateFileName))
                {
                    // for now, we'll keep it simple and not get into XML, JSON or whatever.  It's a simple [key]:[value] per line on the file so we don't need any external libs
                    // even going to resist the urge to make it generic and use Reflection for the properties since we have 1 property right now.  If we reach 3, consider refactor.
                    if (!string.IsNullOrEmpty(line))
                    {
                        var parts = line.Split(':');
                        if (parts != null && parts.Length == 2)
                        {
                            switch (parts[0].ToLower())
                            {
                                case "serialport":
                                    // if the port name has a colon in it, this will break, but I doubt we're going to support WinCE any time soon
                                    state.SerialPort = parts[1];
                                    break;
                            }
                        }
                    }
                }
            }

            return state;
        }

        public static void Clear()
        {
            var state = new State();
            Save(state);
        }

        public static void Save(State state)
        {
            if (state == null)
                throw new System.ArgumentException("State cannot be null");

            using (var writer = File.CreateText(StateFileName))
            {
                writer.WriteLine($"{nameof(state.SerialPort)}:{state.SerialPort}");
            }
        }
    }
}