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
        public PackageManager(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<PackageManager>();
        }

        private readonly ILogger _logger;
        private string _mpakExtension = ".mpak";
        private string _info_json = "info.json";

        public async Task<string> CreatePackage(string projectPath, string osVersion, string mpakName, string globPath)
        {
            if (!string.IsNullOrWhiteSpace(projectPath))
            {
                // build project
                var projectPathInfo = new FileInfo(projectPath);
                BuildProject(projectPath);

                // get target framework and App.dll path
                var targetFramework = GetProjectTargetFramework(projectPath);
                if (!string.IsNullOrWhiteSpace(projectPathInfo?.DirectoryName))
                {
                    var targetFrameworkDir = Path.Combine(projectPathInfo.DirectoryName, "bin", "Debug", targetFramework);
                    string appDllPath = Path.Combine(targetFrameworkDir, "App.dll");

                    await TrimDependencies(appDllPath, osVersion);

                    // create mpak file
                    var postlinkBinDir = Path.Combine(targetFrameworkDir, "postlink_bin");
                    return CreateMpak(postlinkBinDir, mpakName, osVersion, globPath);
                }
                else
                    return string.Empty;
            }
            else
                return string.Empty;
        }

        void BuildProject(string projectPath)
        {
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
                _logger.LogError("Build failed. Package creation aborted.");
                Environment.Exit(0);
            }
        }

        string GetProjectTargetFramework(string projectPath)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(projectPath);
            var targetFramework =
                doc?.DocumentElement?.SelectSingleNode("/Project/PropertyGroup/TargetFramework")?.InnerText;

            return string.IsNullOrEmpty(targetFramework)
                ? throw new ArgumentException("Could not find TargetFrame in project file.")
                : targetFramework;
        }

        async Task TrimDependencies(string appDllPath, string osVersion)
        {
            FileInfo projectAppDll = new FileInfo(appDllPath);

            if (!string.IsNullOrWhiteSpace(projectAppDll?.DirectoryName))
            {
                var dependencies = AssemblyManager
                .GetDependencies(projectAppDll.Name, projectAppDll.DirectoryName, osVersion)
                .Where(x => x.Contains("App.") == false)
                .ToList();

                await AssemblyManager.TrimDependencies(projectAppDll.Name, projectAppDll.DirectoryName,
                    dependencies, null, null, false, verbose: false);
            }
        }

        string CreateMpak(string postlinkBinDir, string mpakName, string osVersion, string globPath)
        {
            if (string.IsNullOrEmpty(mpakName))
            {
                mpakName = $"{DateTime.UtcNow.ToString("yyyyMMdd")}{DateTime.UtcNow.Millisecond.ToString()}{_mpakExtension}";
            }

            if (!mpakName.EndsWith(_mpakExtension))
            {
                mpakName += _mpakExtension;
            }

            var mpakPath = Path.Combine(Environment.CurrentDirectory, mpakName);

            if (File.Exists(mpakPath))
            {
                Console.WriteLine($"{mpakPath} already exists. Do you wish to overwrite? (Y/n)");

                while (true)
                {
                    Console.Write("> ");
                    var input = Console.ReadKey();
                    Console.WriteLine();
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
            PackageInfo info = new PackageInfo()
            {
                Version = "1",
                OsVersion = osVersion
            };
            var infoJson = JsonSerializer.Serialize(info);
            File.WriteAllText(_info_json, infoJson);
            CreateEntry(archive, _info_json, Path.GetFileName(_info_json));

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