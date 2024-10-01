using Meadow.CLI;

namespace Meadow.Tools;

public class MeadowRoot
{
    private const int NameWidth = 30;
    private const int PropertyWidth = 6;

    private Repo[] _repositories;

    private string[] DefaultRepos = new string[]
        {
            "Meadow.Core",
            "Meadow.Contracts",
            "Meadow.Units",
            "Meadow.Foundation",
            "Meadow.Foundation.CompositeDevices",
            "Meadow.Foundation.Grove",
            "Meadow.Foundation.FeatherWings",
            "Meadow.Foundation.MBus",
            "Meadow.Foundation.mikroBUS",
            "Meadow.Logging",
            "Meadow.Modbus",
            "Meadow.ProjectLab",
            "Meadow.Samples",
            "MQTTnet",
            "Maple",
        };

    public DirectoryInfo Directory { get; }

    public MeadowRoot(ISettingsManager settingsManager)
    {
        var settings = settingsManager.GetPublicSettings();
        if (!settings.ContainsKey("source"))
        {
            throw new Exception("Source root folder not set.  Use `meadow config source`");
        }

        var path = settings["source"].Trim('\'').Trim('"');
        Directory = new DirectoryInfo(path);

        if (!Directory.Exists)
        {
            Directory.Create();
        }

        // look for a file called "repos" in the root.  If it exists, its contents will override the repo list
        var overrideFile = Directory.GetFiles("repos").FirstOrDefault();
        if (overrideFile != null)
        {
            var repos = File.ReadAllLines(overrideFile.FullName).Where(l => l.Length > 0).ToArray();
            _repositories = new Repo[repos.Length];
            for (var i = 0; i < repos.Length; i++)
            {
                _repositories[i] = new Repo(Path.Combine(Directory.FullName, repos[i]));
            }
        }
        else
        {
            _repositories = new Repo[DefaultRepos.Length];
            for (var i = 0; i < DefaultRepos.Length; i++)
            {
                _repositories[i] = new Repo(Path.Combine(Directory.FullName, DefaultRepos[i]));
            }
        }
    }

    public void Clone()
    {
        foreach (var repo in _repositories)
        {
            // only clone if we don't have it
            if (Path.Exists(repo.Folder))
            {
                Console.WriteLine($"[{repo.Name}] already exists", ConsoleColor.White);
            }
            else
            {
                Console.WriteLine($"cloning [{repo.Name}]...", ConsoleColor.White);
                repo.Clone();
            }
        }
    }

    public void Pull()
    {
        foreach (var repo in _repositories)
        {
            Console.WriteLine($"pulling [{repo.Name}]...", ConsoleColor.White);
            repo.Pull();
        }
    }

    public void Fetch()
    {
        foreach (var repo in _repositories)
        {
            Console.WriteLine($"fetching [{repo.Name}]...", ConsoleColor.White);
            repo.Fetch();
        }
    }

    public void Checkout(string branch)
    {
        Console.WriteLine($"Checking out [{branch}]...", ConsoleColor.White);

        foreach (var repo in _repositories)
        {
            Console.WriteLine($"  repo [{repo.Name}]...", ConsoleColor.White);
            repo.Checkout(branch);
        }
    }

    public void Status()
    {
        Console.WriteLine($"Source root is '{Directory.FullName}{Environment.NewLine}", ConsoleColor.White);

        Console.WriteLine($"| {"Repo name".PadRight(NameWidth)} | {"Current branch".PadRight(NameWidth)} | {"Ahead".PadRight(PropertyWidth)} | {"Behind".PadRight(PropertyWidth)} | {"Dirty".PadRight(PropertyWidth)} |");
        Console.WriteLine($"| {"".PadRight(NameWidth, '-')} | {"".PadRight(NameWidth, '-')} | {"".PadRight(PropertyWidth, '-')} | {"".PadRight(PropertyWidth, '-')} | {"".PadRight(PropertyWidth, '-')} |");

        if (_repositories.Length == 0)
        {
            Console.WriteLine("| No git repos found");
            return;
        }

        foreach (var repo in _repositories)
        {
            var name = repo.Name.PadRight(NameWidth);
            var friendly = repo.Branch.PadRight(NameWidth);
            var ahead = $"{repo.Ahead}".PadRight(PropertyWidth);
            var behind = $"{repo.Behind}".PadRight(PropertyWidth);
            var dirty = $"{repo.IsDirty}".PadRight(PropertyWidth);

            Console.Write("| ");
            ConsoleWriteWithColor(name, ConsoleColor.White);
            ConsoleWriteWithColor(friendly, ahead[0] == ' ' ? ConsoleColor.Yellow : ConsoleColor.White);
            ConsoleWriteWithColor(ahead, ahead[0] == '0' ? ConsoleColor.White : ConsoleColor.Cyan);
            ConsoleWriteWithColor(behind, behind[0] == '0' ? ConsoleColor.White : ConsoleColor.Cyan);
            ConsoleWriteWithColor(dirty, repo.IsDirty ? ConsoleColor.Red : ConsoleColor.White);
            Console.WriteLine();
        }

    }

    private void ConsoleWriteWithColor(string text, ConsoleColor color)
    {
        if (text.Length > NameWidth)
        {
            text = string.Concat(text.AsSpan(0, NameWidth - 3), "...");
        }

        Console.ForegroundColor = color;
        Console.Write(text);
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write(" | ");
    }
}
