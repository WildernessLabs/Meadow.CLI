using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using Mono.Collections.Generic;
using System;
using System.Diagnostics;
using System.Reflection;

namespace Meadow.CLI.Core.DeviceManagement
{
    //https://github.com/xamarin/xamarin-macios/blob/main/tools/mtouch/Assembly.mtouch.cs#L54

    public static class AssemblyManager
    {
        private static readonly List<string> dependencyMap = new List<string>();
        private static string? fileName;

        private static string meadow_override_path = null;

        public static IEnumerable<string> LinkDependencies(string file, string path, List<string> dependencies, bool includePdbs)
        {
            var prelink_dir = Path.Combine (path, "prelink_bin");
            var prelink_app = Path.Combine (prelink_dir, file);

            if (Directory.Exists(prelink_dir))
                Directory.Delete(prelink_dir, recursive: true);
            Directory.CreateDirectory (prelink_dir);
            File.Copy (Path.Combine(path, file), prelink_app, overwrite: true);
            foreach (var dependency in dependencies) {
                File.Copy (dependency,
                            Path.Combine(prelink_dir, Path.GetFileName(dependency)),
                            overwrite: true);
                if (includePdbs)
                {
                    var pdbFile = Path.ChangeExtension(dependency, "pdb");
                    if (File.Exists(pdbFile))
                        File.Copy (pdbFile,
                            Path.Combine(prelink_dir, Path.GetFileName(pdbFile)),
                            overwrite: true);
                }
            }

            var postlink_dir = Path.Combine (path, "postlink_bin");

            if (Directory.Exists(postlink_dir))
                Directory.Delete(postlink_dir, recursive: true);
            Directory.CreateDirectory (postlink_dir);

            var base_path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var monolinker_path = Path.Combine (base_path, "resources/monolinker.exe");
            var descriptor_path = Path.Combine (base_path, "resources/meadow_link.xml");
            var monolinker_args = $"-x {descriptor_path} -l all -b true -g false -c link -o {postlink_dir} -a {prelink_app} -d {prelink_dir}";

            using (var process = new Process())
            {
                process.StartInfo.FileName = monolinker_path;
                process.StartInfo.Arguments = monolinker_args;
                process.StartInfo.UseShellExecute = true;
                process.StartInfo.RedirectStandardOutput = false;
                process.Start();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new Exception ("Monolinker execution failed!");
            }

            // copy netstandard.dll over, it is needed and seems to get linked out
            File.Copy (Path.Combine(prelink_dir, "netstandard.dll"), Path.Combine(postlink_dir, "netstandard.dll"));

            return Directory.EnumerateFiles(postlink_dir);
        }

        public static List<string> GetDependencies(string file, string path, string osVersion)
        {
            meadow_override_path = Path.Combine(DownloadManager.FirmwareDownloadsFilePathRoot, osVersion, "meadow_assemblies");
            if (!Directory.Exists(meadow_override_path))
                meadow_override_path = path;

            dependencyMap.Clear();

            var refs = GetAssemblyNameReferences(fileName = file, path);

            var dependencies = GetDependencies(refs, dependencyMap, path);

            return dependencies;
        }

        static (Collection<AssemblyNameReference>?, string?) GetAssemblyNameReferences(string fileName, string path)
        {
            string? ResolvePath (string fileName, string path)
            {
                string attempted_path = Path.Combine(path, fileName);
                if (Path.GetExtension(fileName) != ".exe" &&
                    Path.GetExtension(fileName) != ".dll")
                {
                    attempted_path += ".dll";
                }
                return File.Exists(attempted_path) ? attempted_path : null;
            }

            string? resolved_path = ResolvePath (fileName, meadow_override_path) ?? ResolvePath (fileName, path);

            if (resolved_path is null)
                return (null, null);

            Collection<AssemblyNameReference> references;

            using (var definition = AssemblyDefinition.ReadAssembly(resolved_path))
            {
                references = definition.MainModule.AssemblyReferences;
            }
            return (references, resolved_path);
        }

        static List<string> GetDependencies((Collection<AssemblyNameReference>?, string?) references, List<string> dependencyMap, string folderPath)
        {
            if (dependencyMap.Contains(references.Item2))
                return dependencyMap;

            dependencyMap.Add(references.Item2);

            foreach (var ar in references.Item1)
            {
                var namedRefs = GetAssemblyNameReferences(ar.Name, folderPath);

                if (namedRefs.Item1 == null)
                    continue;

                GetDependencies(namedRefs, dependencyMap, folderPath);
            }

            return dependencyMap;
        }
    }
}
