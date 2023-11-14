﻿using Microsoft.Extensions.Logging;
using Mono.Cecil;
using Mono.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace Meadow.CLI;

public partial class PackageManager
{
    private const string IL_LINKER_DIR = "lib";
    public const string PostLinkDirectoryName = "postlink_bin";
    public const string PreLinkDirectoryName = "prelink_bin";
    public const string PackageOutputDirectoryName = "mpak";


    private readonly List<string> dependencyMap = new();

    private string? _meadowAssembliesPath;

    public string? MeadowAssembliesPath
    {
        get
        {
            if (_meadowAssembliesPath == null)
            {
                // for now we only support F7
                // TODO: add switch and support for other platforms
                var store = _fileManager.Firmware["Meadow F7"];
                if (store != null)
                {
                    store.Refresh();

                    if (RuntimeVersion == null)
                    {
                        if (store.DefaultPackage != null)
                        {
                            var defaultPackage = store.DefaultPackage;

                            if (defaultPackage.BclFolder != null)
                            {
                                _meadowAssembliesPath = defaultPackage.GetFullyQualifiedPath(defaultPackage.BclFolder);
                            }
                        }
                    }
                    else
                    {
                        var existing = store.FirstOrDefault(p => p.Version == RuntimeVersion);

                        if (existing == null || existing.BclFolder == null) return null;

                        _meadowAssembliesPath = existing.GetFullyQualifiedPath(existing.BclFolder);
                    }
                }
            }

            return _meadowAssembliesPath;
        }
    }

    public List<string>? AssemblyDependencies { get; set; }

    public IEnumerable<string>? TrimmedDependencies { get; set; }
    public bool Trimmed { get; set; } = false;

    public string? RuntimeVersion { get; set; }

    public async Task<IEnumerable<string>?> TrimDependencies(FileInfo file, List<string> dependencies, IList<string>? noLink, ILogger? logger, bool includePdbs, bool verbose = false, string? linkerOptions = null)
    {
        var directoryName = file.DirectoryName;
        if (!string.IsNullOrEmpty(directoryName))
        {
            var fileName = file.Name;
            var prelink_dir = Path.Combine(directoryName, PreLinkDirectoryName);
            var prelink_app = Path.Combine(prelink_dir, fileName);
            var prelink_os = Path.Combine(prelink_dir, "Meadow.dll");

            if (Directory.Exists(prelink_dir))
            {
                Directory.Delete(prelink_dir, recursive: true);
            }

            Directory.CreateDirectory(prelink_dir);
            File.Copy(file.FullName, prelink_app, overwrite: true);

            foreach (var dependency in dependencies)
            {
                File.Copy(dependency,
                            Path.Combine(prelink_dir, Path.GetFileName(Path.GetFileName(dependency))),
                            overwrite: true);

                if (includePdbs)
                {
                    var pdbFile = Path.ChangeExtension(dependency, "pdb");
                    if (File.Exists(pdbFile))
                    {
                        File.Copy(pdbFile,
                            Path.Combine(prelink_dir, Path.GetFileName(pdbFile)),
                            overwrite: true);
                    }
                }
            }

            var postlink_dir = Path.Combine(directoryName, PostLinkDirectoryName);
            if (Directory.Exists(postlink_dir))
            {
                Directory.Delete(postlink_dir, recursive: true);
            }
            Directory.CreateDirectory(postlink_dir);

            var base_path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(base_path))
            {
                var illinker_path = Path.Combine(base_path, IL_LINKER_DIR, "illink.dll");
                var descriptor_path = Path.Combine(base_path, IL_LINKER_DIR, "meadow_link.xml");

                if (!File.Exists(illinker_path))
                {
                    throw new FileNotFoundException("Cannot run trimming operation. illink.dll not found.");
                }

                if (linkerOptions != null)
                {
                    var fi = new FileInfo(linkerOptions);

                    if (fi.Exists)
                    {
                        logger?.LogInformation($"Using linker options from '{linkerOptions}'");
                    }
                    else
                    {
                        logger?.LogWarning($"Linker options file '{linkerOptions}' not found");
                    }
                }


                // add in any run-time no-link arguments
                var no_link_args = string.Empty;
                if (noLink != null)
                {
                    // no-link options want just the assembly name (i.e. no ".dll" extension)
                    no_link_args = string.Join(" ", noLink.Select(o => $"-p copy \"{o.Replace(".dll", string.Empty)}\""));
                }

                var monolinker_args = $"\"{illinker_path}\" -x \"{descriptor_path}\" {no_link_args}  --skip-unresolved --deterministic --keep-facades true --ignore-descriptors true -b true -c link -o \"{postlink_dir}\" -r \"{prelink_app}\" -a \"{prelink_os}\" -d \"{prelink_dir}\"";

                logger?.LogInformation($"Trimming assemblies associated with {fileName} to reduce upload size (this may take a few seconds)...");
                if (!string.IsNullOrWhiteSpace(no_link_args))
                {
                    logger?.LogInformation($"no-link args:'{no_link_args}'");
                }

                using (var process = new Process())
                {
                    process.StartInfo.FileName = "dotnet";
                    process.StartInfo.Arguments = monolinker_args;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.Start();

                    // To avoid deadlocks, read the output stream first and then wait
                    string stdOutReaderResult;
                    using (StreamReader stdOutReader = process.StandardOutput)
                    {
                        stdOutReaderResult = await stdOutReader.ReadToEndAsync();
                        if (verbose)
                        {
                            logger?.LogInformation("StandardOutput Contains: " + stdOutReaderResult);
                        }

                    }

                    string stdErrorReaderResult;
                    using (StreamReader stdErrorReader = process.StandardError)
                    {
                        stdErrorReaderResult = await stdErrorReader.ReadToEndAsync();
                        if (!string.IsNullOrEmpty(stdErrorReaderResult))
                        {
                            logger?.LogInformation("StandardError Contains: " + stdErrorReaderResult);
                        }
                    }

                    process.WaitForExit(60000);
                    if (process.ExitCode != 0)
                    {
                        logger?.LogDebug($"Trimming failed - ILLinker execution error!\nProcess Info: {process.StartInfo.FileName} {process.StartInfo.Arguments} \nExit Code: {process.ExitCode}");
                        throw new Exception("Trimming failed");
                    }
                }

                return Directory.EnumerateFiles(postlink_dir);
            }
            else
            {
                throw new DirectoryNotFoundException("Trimming failed: base_path is invalid");
            }
        }
        else
        {
            throw new ArgumentException("Trimming failed: file.DirectoryName is invalid");
        }
    }

    public List<string> GetDependencies(FileInfo file)
    {
        dependencyMap.Clear();

        var directoryName = file.DirectoryName;
        if (!string.IsNullOrEmpty(directoryName))
        {
            var refs = GetAssemblyReferences(file.Name, directoryName);

            var dependencies = GetDependencies(refs, dependencyMap, directoryName);

            return dependencies;
        }
        else
        {
            return new();
        }
    }

    private (Collection<AssemblyNameReference>? References, string? ResolvedPath) GetAssemblyReferences(string fileName, string path)
    {
        static string? ResolvePath(string fileName, string path)
        {
            string attemptedPath = Path.Combine(path, fileName);
            if (Path.GetExtension(fileName) != ".exe"
                && Path.GetExtension(fileName) != ".dll")
            {
                attemptedPath += ".dll";
            }
            return File.Exists(attemptedPath) ? attemptedPath : null;
        }

        if (!string.IsNullOrEmpty(MeadowAssembliesPath))
        {
            string? resolvedPath = ResolvePath(fileName, MeadowAssembliesPath) ?? ResolvePath(fileName, path);

            if (resolvedPath is null)
            {
                return (null, null);
            }

            Collection<AssemblyNameReference> references;

            try
            {
                using (var definition = AssemblyDefinition.ReadAssembly(resolvedPath))
                {
                    references = definition.MainModule.AssemblyReferences;
                }
                return (references, resolvedPath);
            }
            catch (Exception ex)
            {
                // Handle or log the exception appropriately
                Console.WriteLine($"Error reading assembly: {ex.Message}");
                return (null, null);
            }
        }
        else
        {
            return (null, null);
        }
    }

    private List<string> GetDependencies((Collection<AssemblyNameReference>? References, string? ResolvedPath) references, List<string> dependencyMap, string folderPath)
    {
        if (references.ResolvedPath == null || dependencyMap.Contains(references.ResolvedPath))
            return dependencyMap;

        dependencyMap.Add(references.ResolvedPath);

        if (references.References != null)
        {
            foreach (var ar in references.References)
            {
                var namedRefs = GetAssemblyReferences(ar.Name, folderPath);

                if (namedRefs.References == null)
                    continue;

                GetDependencies(namedRefs, dependencyMap, folderPath);
            }
        }

        return dependencyMap;
    }
}