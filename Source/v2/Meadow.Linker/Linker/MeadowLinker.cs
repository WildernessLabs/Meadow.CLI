using Microsoft.Extensions.Logging;
using Mono.Cecil;
using Mono.Collections.Generic;
using System.Reflection;

namespace Meadow.Linker;

public class MeadowLinker
{
    private const string IL_LINKER_DIR = "lib";
    private const string IL_LINKER_DLL = "illink.dll";
    private const string MEADOW_LINK_XML = "meadow_link.xml";

    private const string PostLinkDirectoryName = "postlink_bin";
    private const string PreLinkDirectoryName = "prelink_bin";

    readonly ILLinker _linker;
    readonly ILogger? _logger;

    //ToDo ... might need to make this a property or pass it in when used
    private readonly string _meadowAssembliesPath;

    public MeadowLinker(string meadowAssembliesPath, ILogger? logger = null)
    {
        _meadowAssembliesPath = meadowAssembliesPath;
        _logger = logger;
        _linker = new ILLinker(logger);
    }

    public async Task Trim(
        FileInfo meadowAppFile,
        bool includePdbs = false,
        IList<string>? noLink = null)
    {
        var dependencies = MapDependencies(meadowAppFile);

        CopyDependenciesToPreLinkFolder(meadowAppFile, dependencies, includePdbs);

        await TrimMeadowApp(meadowAppFile, noLink);
    }

    public List<string> MapDependencies(FileInfo meadowAppFile)
    {
        //get all dependencies in meadowAppFile and exclude the Meadow App 
        var dependencyMap = new List<string>();

        var appRefs = GetAssemblyReferences(meadowAppFile.FullName);
        return GetDependencies(meadowAppFile.FullName, appRefs, dependencyMap, meadowAppFile.DirectoryName);
    }

    public void CopyDependenciesToPreLinkFolder(
        FileInfo meadowApp,
        List<string> dependencies,
        bool includePdbs)
    {
        //set up the paths
        var prelinkDir = Path.Combine(meadowApp.DirectoryName!, PreLinkDirectoryName);
        var postlinkDir = Path.Combine(meadowApp.DirectoryName!, PostLinkDirectoryName);

        //create output directories
        CreateEmptyDirectory(prelinkDir);
        CreateEmptyDirectory(postlinkDir);

        //copy meadow app
        File.Copy(meadowApp.FullName, Path.Combine(prelinkDir, meadowApp.Name), overwrite: true);

        //copy dependencies and optional pdbs from the local folder and the meadow assemblies folder
        foreach (var dependency in dependencies)
        {
            var destination = Path.Combine(prelinkDir, Path.GetFileName(dependency));
            File.Copy(dependency, destination, overwrite: true);

            if (includePdbs)
            {
                var pdbFile = Path.ChangeExtension(dependency, "pdb");
                if (File.Exists(pdbFile))
                {
                    destination = Path.ChangeExtension(destination, "pdb");
                    File.Copy(pdbFile, destination, overwrite: true);
                }
            }
        }
    }

    private async Task<IEnumerable<string>> TrimMeadowApp(
        FileInfo file,
        IList<string>? noLink)
    {
        //set up the paths
        var prelink_dir = Path.Combine(file.DirectoryName!, PreLinkDirectoryName);
        var postlink_dir = Path.Combine(file.DirectoryName!, PostLinkDirectoryName);
        var prelink_app = Path.Combine(prelink_dir, file.Name);
        var base_path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var illinker_path = Path.Combine(base_path!, IL_LINKER_DIR, IL_LINKER_DLL);
        var descriptor_path = Path.Combine(base_path!, IL_LINKER_DIR, MEADOW_LINK_XML);

        //prepare _linker arguments
        var no_link_args = noLink != null ? string.Join(" ", noLink.Select(o => $"-p copy \"{o}\"")) : string.Empty;

        try
        {
            //link the apps
            await _linker.RunILLink(illinker_path, descriptor_path, no_link_args, prelink_app, prelink_dir, postlink_dir);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error trimming Meadow app");
        }

        return Directory.EnumerateFiles(postlink_dir);
    }

    /// <summary>
    /// This method recursively gets all dependencies for the given assembly
    /// </summary>
    private List<string> GetDependencies(string assemblyPath, Collection<AssemblyNameReference> assemblyReferences, List<string> dependencyMap, string appDir)
    {
        if (dependencyMap.Contains(assemblyPath))
        {   //already have this assembly mapped
            return dependencyMap;
        }

        dependencyMap.Add(assemblyPath);

        foreach (var reference in assemblyReferences)
        {
            var fullPath = FindAssemblyFullPath(reference.Name, appDir, _meadowAssembliesPath);

            Collection<AssemblyNameReference> namedRefs = default!;

            if (fullPath == null)
            {
                continue;
            }
            namedRefs = GetAssemblyReferences(fullPath);

            //recursive!
            dependencyMap = GetDependencies(fullPath!, namedRefs!, dependencyMap, appDir);
        }

        return dependencyMap.Where(x => x.Contains("App.") == false).ToList();
    }

    static string? FindAssemblyFullPath(string fileName, string localPath, string meadowAssembliesPath)
    {
        //Assembly may not have a file extension, add .dll if it doesn't
        if (Path.GetExtension(fileName) != ".exe" &&
            Path.GetExtension(fileName) != ".dll")
        {
            fileName += ".dll";
        }

        //meadow assemblies path
        if (File.Exists(Path.Combine(meadowAssembliesPath, fileName)))
        {
            return Path.Combine(meadowAssembliesPath, fileName);
        }

        //local path
        if (File.Exists(Path.Combine(localPath, fileName)))
        {
            return Path.Combine(localPath, fileName);
        }

        return null;
    }

    private Collection<AssemblyNameReference> GetAssemblyReferences(string assemblyPath)
    {
        using var definition = AssemblyDefinition.ReadAssembly(assemblyPath);
        return definition.MainModule.AssemblyReferences;
    }

    private void CreateEmptyDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
        }
        Directory.CreateDirectory(directoryPath);
    }
}