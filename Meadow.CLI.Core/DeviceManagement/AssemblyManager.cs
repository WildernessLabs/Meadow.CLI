using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using Mono.Collections.Generic;
using System;

namespace Meadow.CLI.Core.DeviceManagement
{
    //https://github.com/xamarin/xamarin-macios/blob/main/tools/mtouch/Assembly.mtouch.cs#L54

    public static class AssemblyManager
    {
        private static readonly List<string> dependencyMap = new List<string>();
        private static string? fileName;

        private static string meadow_override_path = null;

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
