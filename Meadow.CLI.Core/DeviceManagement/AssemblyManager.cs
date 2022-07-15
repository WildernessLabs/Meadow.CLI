﻿using System.Collections.Generic;
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
        private static readonly List<string> dependencyMap = new();

        private static string? meadowAssembliesPath = null;

        public static IEnumerable<string> LinkDependencies(string file, string path, List<string> dependencies, bool includePdbs)
        {
            var prelink_dir = Path.Combine (path, "prelink_bin");
            var prelink_app = Path.Combine (prelink_dir, file);
            var prelink_os = Path.Combine (prelink_dir, "Meadow.dll");

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
            var illinker_path = Path.Combine (base_path, "Resources/illink.dll");
            var descriptor_path = Path.Combine (base_path, "Resources/meadow_link.xml");
            var monolinker_args = $"{illinker_path} -x {descriptor_path} --skip-unresolved --deterministic --keep-facades true --ignore-descriptors true -b true -c link -o {postlink_dir} -r {prelink_app} -a {prelink_os} -d {prelink_dir}";
            Console.WriteLine(monolinker_args);
            using (var process = new Process())
            {
                process.StartInfo.FileName = "dotnet";
                process.StartInfo.Arguments = monolinker_args;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = false;
                process.Start();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new Exception ("ILLinker execution failed!");
            }

            return Directory.EnumerateFiles(postlink_dir);
        }

        public static List<string> GetDependencies(string file, string path, string osVersion)
        {
            meadowAssembliesPath = Path.Combine(DownloadManager.FirmwareDownloadsFilePathRoot, osVersion, "meadow_assemblies");

            if (!Directory.Exists(meadowAssembliesPath))
            {   //try crawling back to the last minor version ... ToDo osVersion should be a proper object
                var lastMinorVersion = osVersion.Substring(0, osVersion.LastIndexOf('.')) + ".0";
                meadowAssembliesPath = Path.Combine(DownloadManager.FirmwareDownloadsFilePathRoot, lastMinorVersion, "meadow_assemblies");
            }

            if (!Directory.Exists(meadowAssembliesPath))
            {
                throw new FileNotFoundException($"Unable to locate local Meadow assemblies for v{osVersion}. Run `meadow download os` to download the latest meadow OS and libraries.");
            }

            dependencyMap.Clear();

            var refs = GetAssemblyNameReferences(file, path);

            var dependencies = GetDependencies(refs, dependencyMap, path);

            return dependencies;
        }

        static (Collection<AssemblyNameReference>?, string?) GetAssemblyNameReferences(string fileName, string path)
        {
            static string? ResolvePath(string fileName, string path)
            {
                string attempted_path = Path.Combine(path, fileName);
                if (Path.GetExtension(fileName) != ".exe" &&
                    Path.GetExtension(fileName) != ".dll")
                {
                    attempted_path += ".dll";
                }
                return File.Exists(attempted_path) ? attempted_path : null;
            }

            //ToDo - is it ever correct to fall back to the root path without a version?
            string? resolved_path = ResolvePath(fileName, meadowAssembliesPath) ?? ResolvePath(fileName, path);

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