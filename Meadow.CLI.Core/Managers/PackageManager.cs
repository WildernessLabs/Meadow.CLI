using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;
using GlobExpressions;
using Meadow.CLI.Core.DeviceManagement;

namespace Meadow.CLI.Core
{
    public class PackageManager
    {
        private List<string> _firmwareFilesExclude;

        public PackageManager(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<PackageManager>();
            _firmwareFilesExclude = new List<string>()
            {
                "Meadow.OS.bin".ToLower()
            };
        }

        private readonly ILogger _logger;

        public async Task<string> CreatePackage(string projectPath, string osVersion, string mpakName, string globPath)
        {
            var projectPathInfo = new FileInfo(projectPath);

            BuildProject(projectPath);
            var targetFramework = GetProjectTargetFramework(projectPath);

            var targetFrameworkDir = Path.Combine(projectPathInfo.DirectoryName, "bin", "Debug", targetFramework);
            string appDllPath = Path.Combine(targetFrameworkDir, "App.dll");

            await TrimDependencies(appDllPath, osVersion);

            var postlinkBinDir = Path.Combine(targetFrameworkDir, "postlink_bin");

            return CreateMpak(postlinkBinDir, mpakName, osVersion, globPath);
        }

        void BuildProject(string projectPath)
        {
            // run dotnet build on the project file
            var proc = new Process();
            proc.StartInfo.FileName = "dotnet";
            proc.StartInfo.Arguments = $"build {projectPath}";

            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.ErrorDialog = false;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.UseShellExecute = false;

            proc.ErrorDataReceived += (sendingProcess, errorLine) => Console.WriteLine(errorLine.Data);
            proc.OutputDataReceived += (sendingProcess, dataLine) => Console.WriteLine(dataLine.Data);

            proc.Start();
            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();

            proc.WaitForExit();
            var exitCode = proc.ExitCode;
            proc.Close();

            if (exitCode != 0)
            {
                _logger.LogError("Package creation aborted. Build failed.");
                Environment.Exit(0);
            }
        }

        string GetProjectTargetFramework(string projectPath)
        {
            // open the project file to get the TargetFramework value
            XmlDocument doc = new XmlDocument();
            doc.Load(projectPath);
            return doc?.DocumentElement?.SelectSingleNode("/Project/PropertyGroup/TargetFramework")?.InnerText;
        }

        async Task TrimDependencies(string appDllPath, string osVersion)
        {
            FileInfo projectAppDll = new FileInfo(appDllPath);

            var dependencies = AssemblyManager
                .GetDependencies(projectAppDll.Name, projectAppDll.DirectoryName, osVersion)
                .Where(x => x.Contains("App.") == false)
                .ToList();

            await AssemblyManager.TrimDependencies(projectAppDll.Name, projectAppDll.DirectoryName, dependencies, null,
                null, false, verbose: false);
        }

        string CreateMpak(string postlinkBinDir, string mpakName, string osVersion, string globPath)
        {
            if (string.IsNullOrEmpty(mpakName))
            {
                mpakName = $"{DateTime.UtcNow.ToString("yyyyMMdd")}{DateTime.UtcNow.Millisecond.ToString()}.mpak";
            }

            if (!mpakName.EndsWith(".mpak"))
            {
                mpakName += ".mpak";
            }

            var mpakPath = Path.Combine(Environment.CurrentDirectory, mpakName);

            if (File.Exists(mpakPath))
            {
                Console.WriteLine($"{mpakPath} already exists. Do you with to overwrite (Y/n)");

                while (true)
                {
                    Console.Write("> ");
                    var input = Console.ReadKey();
                    switch (input.Key)
                    {
                        case ConsoleKey.Y:
                            File.Delete(mpakPath);
                            break;
                        case ConsoleKey.N:
                            Environment.Exit(0);
                            break;
                        default:
                            continue;
                    }

                    break;
                }
            }

            var appFiles = Glob.Files(postlinkBinDir, globPath, GlobOptions.CaseInsensitive).ToArray();
            using var archive = ZipFile.Open(mpakPath, ZipArchiveMode.Create);

            foreach (var fPath in appFiles)
            {
                CreateEntry(archive, Path.Combine(postlinkBinDir, fPath), Path.Combine("app", Path.GetFileName(fPath)));
            }

            // write a metadata file info.json in the mpak
            var info = new { v = 1, osVersion };
            var infoJson = JsonSerializer.Serialize(info);
            File.WriteAllText("info.json", infoJson);
            CreateEntry(archive, "info.json", Path.GetFileName("info.json"));

            return mpakPath;
        }

        void CreateEntry(ZipArchive archive, string fromFile, string entryPath)
        {
            // Windows '\' Path separator character will be written to the zip which meadow os does not properly unpack
            //  See: https://github.com/dotnet/runtime/issues/41914
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                entryPath = entryPath.Replace('\\', '/');
            }

            archive.CreateEntryFromFile(fromFile, entryPath);
        }
    }
}