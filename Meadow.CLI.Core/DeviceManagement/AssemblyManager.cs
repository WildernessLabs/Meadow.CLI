﻿using System;
using Mono.Collections.Generic;
using System.IO;
using Mono.Cecil;
using System.Collections.Generic;

namespace MeadowCLI.DeviceManagement
{
    //https://github.com/xamarin/xamarin-macios/blob/main/tools/mtouch/Assembly.mtouch.cs#L54

    public static class AssemblyManager
    {
        static List<string> dependencyMap = new List<string>();
        static string folderPath;
        static string fileName;

        public static List<string> GetDependencies(string file, string path)
        {
            dependencyMap.Clear();

            var refs = GetAssemblyNameReferences(fileName = file, folderPath = path);

            var dependencies = GetDependencies(refs, dependencyMap);

            for (int i = 0; i < dependencies.Count; i++)
            {
                dependencies[i] += ".dll";
            }
            return dependencies;
        }

        static Collection<AssemblyNameReference> GetAssemblyNameReferences(string fileName, string path = null)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                fileName = Path.Combine(path, fileName);
            }

            if (Path.GetExtension(fileName) != ".exe")
            {
                fileName += ".dll";
            }

            Collection<AssemblyNameReference> references;

            if (File.Exists(fileName) == false)
            {
                Console.WriteLine($"Could not find {fileName}");
                return null;
            }

            using (var definition = AssemblyDefinition.ReadAssembly(fileName))
            {
                references = definition.MainModule.AssemblyReferences;
            }
            return references;
        }

        static List<string> GetDependencies(Collection<AssemblyNameReference> references, List<string> dependencyMap)
        {
            foreach (var ar in references)
            {
                if (!dependencyMap.Contains(ar.Name))
                {
                    var namedRefs = GetAssemblyNameReferences(ar.Name, folderPath);

                    if (namedRefs == null)
                    {
                        continue;
                    }

                    dependencyMap.Add(ar.Name);

                    GetDependencies(namedRefs, dependencyMap);
                }
            }

            return dependencyMap;
        }
    }
}