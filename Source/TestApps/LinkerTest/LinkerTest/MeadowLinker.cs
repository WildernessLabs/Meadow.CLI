using Microsoft.Extensions.Logging;
using Mono.Cecil;
using Mono.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace LinkerTest;

public class MeadowLinker
{
    private const string IL_LINKER_DIR = "lib";
    public const string PostLinkDirectoryName = "postlink_bin";
    public const string PreLinkDirectoryName = "prelink_bin";

    readonly ILogger? logger;

    private string? MeadowAssembliesPath
    {
        get
        {
            /*
            if (_meadowAssembliesPath == null)
            {
                // for now we only support F7
                // TODO: add switch and support for other platforms
                var store = _fileManager.Firmware["Meadow F7"];
                if (store != null)
                {
                    store.Refresh();
                    if (store.DefaultPackage != null)
                    {
                        var defaultPackage = store.DefaultPackage;

                        if (defaultPackage.BclFolder != null)
                        {
                            _meadowAssembliesPath = defaultPackage.GetFullyQualifiedPath(defaultPackage.BclFolder);
                        }
                    }
                }
            }*/

            return _meadowAssembliesPath;
        }
    }

    private readonly string? _meadowAssembliesPath = @"C:\Users\adria\AppData\Local\WildernessLabs\Firmware\1.5.0.6\meadow_assemblies\";

    public async Task TrimApplication(
        FileInfo applicationFilePath,
        bool includePdbs = false,
        IList<string>? noLink = null)
    {
        if (!applicationFilePath.Exists)
        {
            throw new FileNotFoundException($"{applicationFilePath} not found");
        }

        //get all dependencies in applicationFilePath and exclude the Meadow App 
        var dependencies = GetDependencies(applicationFilePath)
            .Where(x => x.Contains("App.") == false)
            .ToList();

        //run the linker against the dependencies
        await TrimDependencies(
            applicationFilePath,
            dependencies,
            noLink,
            includePdbs);
    }

    public async Task<IEnumerable<string>?> TrimDependencies(
        FileInfo file,
        List<string> dependencies,
        IList<string>? noLink,
        bool includePdbs)
    {
        //set up the paths
        var prelink_dir = Path.Combine(file.DirectoryName!, PreLinkDirectoryName);
        var prelink_app = Path.Combine(prelink_dir, file.Name);
        var prelink_os = Path.Combine(prelink_dir, "Meadow.dll");
        var postlink_dir = Path.Combine(file.DirectoryName!, PostLinkDirectoryName);
        var base_path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var illinker_path = Path.Combine(base_path!, IL_LINKER_DIR, "illink.dll");
        var descriptor_path = Path.Combine(base_path!, IL_LINKER_DIR, "meadow_link.xml");

        //create output directories
        FileSystemHelpers.CleanupAndCreateDirectory(prelink_dir);
        FileSystemHelpers.CleanupAndCreateDirectory(postlink_dir);

        //copy files
        File.Copy(file.FullName, prelink_app, overwrite: true);

        foreach (var dependency in dependencies)
        {
            FileSystemHelpers.CopyFileWithOptionalPdb(dependency, Path.Combine(prelink_dir, Path.GetFileName(dependency)), includePdbs);
        }

        //prepare linker arguments
        var no_link_args = noLink != null ? string.Join(" ", noLink.Select(o => $"-p copy \"{o}\"")) : string.Empty;

        //link the apps
        await TrimApp(illinker_path, descriptor_path, no_link_args, prelink_app, prelink_os, prelink_dir, postlink_dir);

        return Directory.EnumerateFiles(postlink_dir);
    }

    async Task TrimApp(string illinker_path,
        string descriptor_path,
        string no_link_args,
        string prelink_app,
        string prelink_os,
        string prelink_dir,
        string postlink_dir)
    {
        if (!File.Exists(illinker_path))
        {
            throw new FileNotFoundException("Cannot run trimming operation. illink.dll not found.");
        }

        var monolinker_args = $"\"{illinker_path}\" -x \"{descriptor_path}\" {no_link_args}  --skip-unresolved --deterministic --keep-facades true --ignore-descriptors true -b true -c link -o \"{postlink_dir}\" -r \"{prelink_app}\" -a \"{prelink_os}\" -d \"{prelink_dir}\"";

        logger?.Log(LogLevel.Information, "Trimming assemblies");

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

                Console.WriteLine("StandardOutput Contains: " + stdOutReaderResult);

                logger?.Log(LogLevel.Debug, "StandardOutput Contains: " + stdOutReaderResult);
            }

            string stdErrorReaderResult;
            using (StreamReader stdErrorReader = process.StandardError)
            {
                stdErrorReaderResult = await stdErrorReader.ReadToEndAsync();
                if (!string.IsNullOrEmpty(stdErrorReaderResult))
                {
                    logger?.Log(LogLevel.Debug, "StandardError Contains: " + stdErrorReaderResult);
                }
            }

            process.WaitForExit(60000);
            if (process.ExitCode != 0)
            {
                logger?.Log(LogLevel.Debug, $"Trimming failed - ILLinker execution error!\nProcess Info: {process.StartInfo.FileName} {process.StartInfo.Arguments} \nExit Code: {process.ExitCode}");
                throw new Exception("Trimming failed");
            }
        }
    }


    List<string> GetDependencies(FileInfo file)
    {
        var dependencyMap = new List<string>();

        var refs = GetAssemblyNameReferences(file.Name, file.DirectoryName);

        var dependencies = GetDependencies(refs, dependencyMap, file.DirectoryName);

        return dependencies;
    }

    private List<string> GetDependencies((Collection<AssemblyNameReference>? References, string? FullPath) references, List<string> dependencyMap, string folderPath)
    {
        if (dependencyMap.Contains(references.FullPath))
            return dependencyMap;

        dependencyMap.Add(references.FullPath);

        foreach (var ar in references.References)
        {
            var namedRefs = GetAssemblyNameReferences(ar.Name, folderPath);

            if (namedRefs.FullPath == null)
                continue;

            GetDependencies(namedRefs, dependencyMap, folderPath);
        }

        return dependencyMap;
    }

    private (Collection<AssemblyNameReference>? References, string? FullPath) GetAssemblyNameReferences(string fileName, string path)
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
        string? resolved_path = ResolvePath(fileName, MeadowAssembliesPath) ?? ResolvePath(fileName, path);

        if (resolved_path is null)
        {
            return (null, null);
        }

        Collection<AssemblyNameReference> references;

        using (var definition = AssemblyDefinition.ReadAssembly(resolved_path))
        {
            references = definition.MainModule.AssemblyReferences;
        }
        return (references, resolved_path);
    }
}