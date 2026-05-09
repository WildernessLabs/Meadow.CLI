using Meadow.Linker;
using Meadow.Software;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Meadow.CLI;
public partial class BuildManager : IBuildManager
{
    public List<string> BuildErrorText { get; } = new();

    public const string PreLinkDirectoryName = "prelink_bin";
    public const string PostLinkDirectoryName = "postlink_bin";
    public const string PackageOutputDirectoryName = "mpak";

    public const string BuildOptionsFileName = "app.build.yaml";

    private readonly FileManager _fileManager;

    public BuildManager(FileManager fileManager)
    {
        _fileManager = fileManager;
    }

    public List<string> GetDependencies(FileInfo file, string? osVerion)
    {
        var linker = new MeadowLinker(GetAssemblyPathForOS(osVerion));
        return linker.MapDependencies(file);
    }

    internal bool CleanApplication(string projectFilePath, string configuration = "Release", CancellationToken? cancellationToken = null)
    {
        using var proc = new Process();
        proc.StartInfo.FileName = "dotnet";
        proc.StartInfo.Arguments = $"clean \"{projectFilePath}\" -c {configuration}";

        proc.StartInfo.CreateNoWindow = true;
        proc.StartInfo.ErrorDialog = false;
        proc.StartInfo.RedirectStandardError = true;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.UseShellExecute = false;

        var success = true;

        proc.ErrorDataReceived += (sendingProcess, errorLine) =>
        {
            Debug.WriteLine(errorLine.Data);
        };
        proc.OutputDataReceived += (sendingProcess, dataLine) =>
        {
            if (dataLine.Data != null)
            {
                Debug.WriteLine(dataLine.Data);
                if (dataLine.Data.ToLower(CultureInfo.InvariantCulture).Contains("clean failed"))
                {
                    Debug.WriteLine("Clean failed");
                    success = false;
                }
            }
        };

        proc.Start();
        proc.BeginErrorReadLine();
        proc.BeginOutputReadLine();

        proc.WaitForExit();
        var exitCode = proc.ExitCode;
        proc.Close();

        return success;
    }

    public bool BuildApplication(string projectFilePath, string configuration = "Release", bool clean = true, CancellationToken? cancellationToken = null)
    {
        BuildErrorText.Clear();

        if (cancellationToken?.IsCancellationRequested == true)
        {
            return false;
        }

        if (clean && !CleanApplication(projectFilePath, configuration, cancellationToken))
        {
            return false;
        }

        using var proc = new Process();
        proc.StartInfo.FileName = "dotnet";
        proc.StartInfo.Arguments = $"build \"{projectFilePath}\" -c \"{configuration}\"";
        proc.StartInfo.CreateNoWindow = true;
        proc.StartInfo.ErrorDialog = false;
        proc.StartInfo.RedirectStandardError = true;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.UseShellExecute = false;

        string lastMessage = string.Empty;
        proc.OutputDataReceived += (sendingProcess, dataLine) =>
        {
            if (dataLine.Data != null)
            {
                BuildErrorText.Add(dataLine.Data);
                Debug.WriteLine(dataLine.Data);
            }
        };

        proc.Start();
        proc.BeginErrorReadLine();
        proc.BeginOutputReadLine();

        proc.WaitForExit();
        var exitCode = proc.ExitCode;
        proc.Close();

        if (exitCode == 0)
        {
            BuildErrorText.Clear();
        }

        return exitCode == 0;
    }

    public Task TrimApplication(
        FileInfo applicationFilePath,
        string osVersion,
        bool includePdbs = false,
        IEnumerable<string>? noLink = null,
        ILogger? logger = null,
        CancellationToken? cancellationToken = null)
    {
        if (!applicationFilePath.Exists)
        {
            throw new FileNotFoundException($"{applicationFilePath} not found");
        }

        // does a meadow.build.yml file exist?
        var buildOptionsFile = Path.Combine(
            applicationFilePath.DirectoryName ?? string.Empty,
            BuildOptionsFileName);

        if (File.Exists(buildOptionsFile))
        {
            var yaml = File.ReadAllText(buildOptionsFile);
            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .Build();
            var opts = deserializer.Deserialize<BuildOptions>(yaml);

            if (opts != null && opts.Deploy != null)
            {
                if (opts.Deploy.NoLink != null && opts.Deploy.NoLink.Count > 0)
                {
                    noLink = opts.Deploy.NoLink;
                }
                if (opts.Deploy.IncludePDBs != null)
                {
                    includePdbs = opts.Deploy.IncludePDBs.Value;
                }
            }
        }

        if (includePdbs)
        {
            logger?.LogInformation("Including PDBs in the output");
        }

        var linker = new MeadowLinker(GetAssemblyPathForOS(osVersion, logger), logger);

        return linker.Trim(applicationFilePath, includePdbs, noLink);
    }

    public static FileInfo[] GetAvailableBuiltConfigurations(string rootFolder, string appName = "App.dll")
    {
        // check if we were give path to a project file, not the folder of the project file
        if (File.Exists(rootFolder))
        {
            rootFolder = Path.GetDirectoryName(rootFolder) ?? ""; // extreact the folder name or if invalid, use the current directory
        }
        if (!Directory.Exists(rootFolder)) { throw new DirectoryNotFoundException($"Directory not found '{rootFolder}'. Check path to project file."); }

        //see if this is a fully qualified path to the app.dll
        if (File.Exists(Path.Combine(rootFolder, appName)))
        {
            return new FileInfo[] { new(Path.Combine(rootFolder, appName)) };
        }

        // look for a 'bin' folder
        var path = Path.Combine(rootFolder, "bin");
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException($"No 'bin' directory found under '{rootFolder}'. Have you compiled?");

        var files = new List<FileInfo>();
        FindApp(path, files);

        void FindApp(string directory, List<FileInfo> fileList)
        {
            foreach (var dir in Directory.GetDirectories(directory))
            {
                var shortname = Path.GetFileName(dir);

                if (shortname == PostLinkDirectoryName ||
                    shortname == PreLinkDirectoryName ||
                    shortname == PackageOutputDirectoryName)
                {
                    continue;
                }

                var file = Directory.GetFiles(dir).FirstOrDefault(f => string.Compare(Path.GetFileName(f), appName, true) == 0);
                if (file != null)
                {
                    fileList.Add(new FileInfo(file));
                }

                FindApp(dir, fileList);
            }
        }

        return files.ToArray();
    }

    public bool PublishApplication(string projectFilePath, string osVersion, string configuration = "Release", bool clean = true, CancellationToken? cancellationToken = null, string? publishDir = null)
    {
        BuildErrorText.Clear();

        if (cancellationToken?.IsCancellationRequested == true)
        {
            return false;
        }

        if (clean && !CleanApplication(projectFilePath, configuration, cancellationToken))
        {
            return false;
        }

        var meadowAssembliesPath = GetAssemblyPathForOS(osVersion);
        var targetsFile = Path.Combine(Path.GetTempPath(), $"Meadow.Trimming.{Guid.NewGuid():N}.targets");

        // Only override TFM for legacy projects (e.g. netstandard2.1) that can't natively
        // use PublishTrimmed. Modern net10.0+ projects don't need this and it breaks
        // Microsoft.NET.Build.Containers.targets in referenced projects.
        var needsTfmOverride = NeedsTfmOverrideForTrimming(projectFilePath);

        try
        {
            File.WriteAllText(targetsFile, MeadowTrimmingTargets);

            using var proc = new Process();
            proc.StartInfo.FileName = "dotnet";
            // PublishTrimmed is set in the targets file (not here) so it doesn't
            // cascade to netstandard2.1 referenced projects and trigger NETSDK1124.
            // Self-contained + linux-arm RID is required for trimming to include BCL assemblies.
            // PublishDir ensures output goes where the CLI expects (not under a RID subfolder).
            var args = $"publish \"{projectFilePath}\" -c \"{configuration}\"" +
                $" --self-contained -r linux-arm" +
                $" -p:AppendRuntimeIdentifierToOutputPath=false" +
                $" -p:CustomAfterMicrosoftCommonTargets=\"{targetsFile}\"" +
                $" -p:MeadowAssembliesPath=\"{meadowAssembliesPath}\"";

            if (publishDir != null)
            {
                args += $" -p:PublishDir=\"{publishDir}\"";
            }

            if (needsTfmOverride)
            {
                args += $" -p:TargetFrameworkIdentifier=.NETCoreApp" +
                        $" -p:TargetFrameworkVersion=v10.0";
            }

            proc.StartInfo.Arguments = args;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.ErrorDialog = false;
            proc.StartInfo.RedirectStandardError = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.UseShellExecute = false;

            proc.OutputDataReceived += (sendingProcess, dataLine) =>
            {
                if (dataLine.Data != null)
                {
                    BuildErrorText.Add(dataLine.Data);
                    Debug.WriteLine(dataLine.Data);
                }
            };

            proc.Start();
            proc.BeginErrorReadLine();
            proc.BeginOutputReadLine();

            proc.WaitForExit();
            var exitCode = proc.ExitCode;
            proc.Close();

            if (exitCode == 0)
            {
                BuildErrorText.Clear();
            }

            return exitCode == 0;
        }
        finally
        {
            try { File.Delete(targetsFile); } catch { }
        }
    }

    // MSBuild targets injected into dotnet publish to configure trimming for Meadow:
    // - Swaps standard .NET BCL assemblies with Meadow's custom BCL
    // - Treats App.dll as a library root (no entry point required)
    private const string MeadowTrimmingTargets = @"<Project>
  <!-- Enable trimming only for .NETCoreApp projects so netstandard2.1 referenced
       projects don't fail with NETSDK1124. Set as a property (not via -p: on the
       command line) to avoid cascading to all projects in the build graph. -->
  <PropertyGroup Condition=""'$(TargetFrameworkIdentifier)' == '.NETCoreApp'"">
    <PublishTrimmed>true</PublishTrimmed>
    <SuppressTrimAnalysisWarnings>true</SuppressTrimAnalysisWarnings>
    <EnableTrimAnalyzer>false</EnableTrimAnalyzer>
  </PropertyGroup>

  <!-- Run after all runtime pack assemblies are resolved but before the trimmer.
       ComputeFilesToPublish populates ResolvedFileToPublish with self-contained
       runtime assemblies; we need to swap those with Meadow's custom BCL. -->
  <Target Name=""_InjectMeadowAssemblies""
          AfterTargets=""ComputeFilesToPublish""
          BeforeTargets=""_RunILLink""
          Condition=""'$(MeadowAssembliesPath)' != ''"">
    <ItemGroup>
      <_MeadowAssembly Include=""$(MeadowAssembliesPath)/*.dll"" />

      <!-- Remove standard runtime assemblies that Meadow provides custom versions of -->
      <ResolvedFileToPublish Remove=""@(ResolvedFileToPublish)""
          Condition=""Exists('$(MeadowAssembliesPath)/%(Filename)%(Extension)')"" />

      <!-- Add Meadow's custom BCL assemblies -->
      <ResolvedFileToPublish Include=""@(_MeadowAssembly)"">
        <PostprocessAssembly>true</PostprocessAssembly>
        <RelativePath>%(Filename)%(Extension)</RelativePath>
        <CopyToPublishDirectory>PreserveNewest</CopyToPublishDirectory>
      </ResolvedFileToPublish>
    </ItemGroup>
  </Target>

  <!-- Meadow apps are libraries loaded by the Meadow runtime (Meadow.dll has the entry point).
       Override the default root so the trimmer doesn't expect App.dll to have Main(). -->
  <Target Name=""_SetMeadowTrimmerRoots""
          AfterTargets=""PrepareForILLink"">
    <ItemGroup>
      <TrimmerRootAssembly Remove=""@(TrimmerRootAssembly)"" />
      <TrimmerRootAssembly Include=""Meadow"" RootMode=""EntryPoint"" />
      <TrimmerRootAssembly Include=""App"" />
    </ItemGroup>
  </Target>

  <!-- After trimming, the trimmed assemblies are added to ResolvedFileToPublish from linked/.
       Remove the original untrimmed Meadow assemblies to avoid NETSDK1152 duplicate conflicts. -->
  <Target Name=""_CleanupMeadowAssembliesAfterTrimming""
          AfterTargets=""_RunILLink""
          Condition=""'$(MeadowAssembliesPath)' != ''"">
    <ItemGroup>
      <ResolvedFileToPublish Remove=""@(ResolvedFileToPublish)""
          Condition=""$([System.String]::new('%(Identity)').StartsWith('$(MeadowAssembliesPath)'))"" />
    </ItemGroup>
  </Target>
</Project>";

    private static bool NeedsTfmOverrideForTrimming(string projectFilePath)
    {
        // Read the project file to check the TargetFramework.
        // Projects targeting netstandard or netcoreapp3.x and below need the TFM override
        // to enable PublishTrimmed. Modern net5.0+ projects support it natively.
        var projectFile = File.Exists(projectFilePath)
            ? projectFilePath
            : Directory.GetFiles(projectFilePath, "*.csproj").FirstOrDefault();

        if (projectFile != null)
        {
            var content = File.ReadAllText(projectFile);
            // Look for <TargetFramework>netstandard... or <TargetFramework>netcoreapp...
            // IndexOf used instead of Contains(string, StringComparison) for netstandard2.0 compatibility
            if (content.IndexOf("netstandard", StringComparison.OrdinalIgnoreCase) >= 0 ||
                content.IndexOf("netcoreapp", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private string GetAssemblyPathForOS(string? osVersion, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(osVersion))
        {
            osVersion = _fileManager?.Firmware["Meadow F7"]?.DefaultPackage?.Version;
        }

        var store = _fileManager?.Firmware["Meadow F7"];
        if (store != null)
        {
            store.Refresh();

            var package = store.GetClosestLocalPackage(osVersion!);

            logger?.Log(LogLevel.Information, $"Found firmware package for Meadow OS v{osVersion}");

            if (package == null)
            {
                throw new Exception($"No firmware package found for Meadow F7 with version {osVersion}");
            }
            return package.GetFullyQualifiedPath(package.BclFolder);
        }

        throw new Exception("No firmware package(s) found for Meadow F7");
    }
}