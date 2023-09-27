using Meadow.Software;
using System.Diagnostics;
using YamlDotNet.Serialization;

namespace Meadow.Cli;

public partial class PackageManager : IPackageManager
{
    public const string BuildOptionsFileName = "app.build.yaml";

    private FileManager _fileManager;

    public PackageManager(FileManager fileManager)
    {
        _fileManager = fileManager;
    }

    public bool BuildApplication(string projectFilePath, string configuration = "Release")
    {
        var proc = new Process();
        proc.StartInfo.FileName = "dotnet";
        proc.StartInfo.Arguments = $"build {projectFilePath} -c {configuration}";

        proc.StartInfo.CreateNoWindow = true;
        proc.StartInfo.ErrorDialog = false;
        proc.StartInfo.RedirectStandardError = true;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.UseShellExecute = false;

        var success = true;

        proc.ErrorDataReceived += (sendingProcess, errorLine) =>
        {
            // this gets called (with empty data) even on a successful build
            Debug.WriteLine(errorLine.Data);
        };
        proc.OutputDataReceived += (sendingProcess, dataLine) =>
        {
            // look for "Build FAILED"
            if (dataLine.Data != null)
            {
                Debug.WriteLine(dataLine.Data);
                if (dataLine.Data.Contains("Build FAILED", StringComparison.InvariantCultureIgnoreCase))
                {
                    Debug.WriteLine("Build failed");
                    success = false;
                }
            }
            // TODO: look for "X Warning(s)" and "X Error(s)"?
            // TODO: do we want to enable forwarding these messages for "verbose" output?
        };

        proc.Start();
        proc.BeginErrorReadLine();
        proc.BeginOutputReadLine();

        proc.WaitForExit();
        var exitCode = proc.ExitCode;
        proc.Close();

        return success;
    }

    public async Task TrimApplication(
        FileInfo applicationFilePath,
        bool includePdbs = false,
        IList<string>? noLink = null,
        CancellationToken cancellationToken = default)
    {
        if (!applicationFilePath.Exists)
        {
            throw new FileNotFoundException($"{applicationFilePath} not found");
        }

        // does a meadow.build.yml file exist?
        var buildOptionsFile = Path.Combine(
            applicationFilePath.DirectoryName ?? string.Empty,
            BuildOptionsFileName);

        if (File.Exists(buildOptionsFile))
        {
            var yaml = File.ReadAllText(buildOptionsFile);
            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .Build();
            var opts = deserializer.Deserialize<BuildOptions>(yaml);

            if (opts.Deploy.NoLink != null && opts.Deploy.NoLink.Count > 0)
            {
                noLink = opts.Deploy.NoLink;
            }
            if (opts.Deploy.IncludePDBs != null)
            {
                includePdbs = opts.Deploy.IncludePDBs.Value;
            }
        }

        var dependencies = GetDependencies(applicationFilePath)
            .Where(x => x.Contains("App.") == false)
            .ToList();

        await TrimDependencies(
            applicationFilePath,
            dependencies,
            noLink,
            null, // ILogger
            includePdbs,
            verbose: false);
    }

    public async Task DeployApplication()
    {
    }

    public static FileInfo[] GetAvailableBuiltConfigurations(string rootFolder, string appName = "App.dll")
    {
        if (!Directory.Exists(rootFolder)) throw new FileNotFoundException();

        // look for a 'bin' folder
        var path = Path.Combine(rootFolder, "bin");
        if (!Directory.Exists(path)) throw new FileNotFoundException("No 'bin' folder found.  have you compiled?");

        var files = new List<FileInfo>();
        FindApp(path, files);

        void FindApp(string directory, List<FileInfo> fileList)
        {
            foreach (var dir in Directory.GetDirectories(directory))
            {
                var file = Directory.GetFiles(dir).FirstOrDefault(f => string.Compare(Path.GetFileName(f), appName, true) == 0);
                if (file != null)
                {
                    fileList.Add(new FileInfo(file));
                }

                FindApp(dir, fileList);
            }
        }

        return files.ToArray();
    }
}
