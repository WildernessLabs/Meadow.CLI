using System.Collections.Generic;
using System.IO;
using Mono.Cecil;
using Mono.Collections.Generic;

namespace Meadow.CLI.Core.DeviceManagement
{
    //https://github.com/xamarin/xamarin-macios/blob/main/tools/mtouch/Assembly.mtouch.cs#L54

    public static class AssemblyManager
    {
        private static readonly List<string> dependencyMap = new();

        private static string? meadowAssembliesPath = null;

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