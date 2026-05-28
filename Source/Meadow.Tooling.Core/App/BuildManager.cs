using Meadow.Linker;
using Meadow.Software;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        proc.ErrorDataReceived += (sendingProcess, errorLine) =>
        {
            Debug.WriteLine(errorLine.Data);
        };
        proc.OutputDataReceived += (sendingProcess, dataLine) =>
        {
            if (dataLine.Data != null)
            {
                Debug.WriteLine(dataLine.Data);
            }
        };

        proc.Start();
        proc.BeginErrorReadLine();
        proc.BeginOutputReadLine();

        proc.WaitForExit();
        var exitCode = proc.ExitCode;
        proc.Close();

        return exitCode == 0;
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
                // avoids persistent build-server nodes locking outputs (CS2012) across runs
                $" --disable-build-servers" +
                // referenced projects pack-on-build (GeneratePackageOnBuild=true); during an app
                // publish that races the normal build, locking obj outputs (CS2012). An app build
                // never needs the dependency nupkgs, so suppress packing across the whole graph.
                $" -p:GeneratePackageOnBuild=false" +
                $" -p:AppendRuntimeIdentifierToOutputPath=false" +
                $" -p:CustomAfterMicrosoftCommonTargets=\"{targetsFile}\"" +
                $" -p:MeadowAssembliesPath=\"{meadowAssembliesPath}\"";

            // Always set PublishDir explicitly. Without it, --self-contained -r linux-arm
            // creates a {RID}/ subdirectory that doesn't match where the CLI looks for output.
            if (publishDir == null)
            {
                var projDir = File.Exists(projectFilePath)
                    ? Path.GetDirectoryName(Path.GetFullPath(projectFilePath))!
                    : Path.GetFullPath(projectFilePath);

                // Determine TFM directory name from existing build output or from the csproj
                var binDir = Path.Combine(projDir, "bin", configuration);
                var tfmDir = Directory.Exists(binDir)
                    ? Directory.GetDirectories(binDir)
                        .Select(Path.GetFileName)
                        .FirstOrDefault(d => d!.StartsWith("net", StringComparison.OrdinalIgnoreCase))
                    : null;

                // Fallback: read TargetFramework from the csproj (needed on clean builds)
                if (tfmDir == null)
                {
                    tfmDir = GetTargetFrameworkFromProject(projectFilePath);
                }

                publishDir = tfmDir != null
                    ? Path.Combine(binDir, tfmDir, "publish") + Path.DirectorySeparatorChar
                    : Path.Combine(binDir, "publish") + Path.DirectorySeparatorChar;
            }

            args += $" -p:PublishDir=\"{publishDir}\"";

            if (needsTfmOverride)
            {
                // Detect the latest installed .NETCoreApp SDK and target that — .NET is backwards-compatible
                // so the highest available version is the safest choice for legacy projects whose own TFM
                // (netstandard2.1, netcoreappX) can't natively use PublishTrimmed.
                var tfmVersion = GetLatestNetCoreAppVersion();
                args += $" -p:TargetFrameworkIdentifier=.NETCoreApp" +
                        $" -p:TargetFrameworkVersion={tfmVersion}";
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
            proc.ErrorDataReceived += (sendingProcess, dataLine) =>
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
    // - Swaps standard .NET BCL assemblies with Meadow's custom BCL in both
    //   ResolvedFileToPublish (publish output) and ManagedAssemblyToLink (trimmer input)
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

  <!-- After PrepareForILLink populates ManagedAssemblyToLink from the runtime pack,
       swap in Meadow's custom BCL so the trimmer processes the correct assemblies.
       Without this, the trimmer operates on standard .NET CoreLib (which lacks Mono-internal
       types like MonoStackFrame) and its output overwrites our ResolvedFileToPublish injection. -->
  <Target Name=""_InjectMeadowIntoTrimmer""
          AfterTargets=""PrepareForILLink""
          BeforeTargets=""_RunILLink""
          Condition=""'$(MeadowAssembliesPath)' != ''"">
    <ItemGroup>
      <!-- Remove standard BCL assemblies that have Meadow equivalents -->
      <ManagedAssemblyToLink Remove=""@(ManagedAssemblyToLink)""
          Condition=""Exists('$(MeadowAssembliesPath)/%(Filename)%(Extension)')"" />

      <!-- Add Meadow's BCL assemblies as trimmer input -->
      <_MeadowTrimmerAssembly Include=""$(MeadowAssembliesPath)/*.dll"" />
      <ManagedAssemblyToLink Include=""@(_MeadowTrimmerAssembly)"">
        <IsTrimmable>true</IsTrimmable>
      </ManagedAssemblyToLink>

      <!-- Configure trimmer roots: Meadow.dll is the entry point (not App.dll),
           App is a library root, and Meadow.F7 must be rooted because the runtime
           creates device instances via Activator.CreateInstance (reflection). -->
      <TrimmerRootAssembly Remove=""@(TrimmerRootAssembly)"" />
      <TrimmerRootAssembly Include=""Meadow"" RootMode=""EntryPoint"" />
      <TrimmerRootAssembly Include=""App"" />
      <TrimmerRootAssembly Include=""Meadow.F7"" />

      <!-- Root the app's non-BCL deps (drivers, board-support); the runtime instantiates
           some via reflection (e.g. IMeadowAppEmbeddedHardwareProvider.Create) so they
           must not be trimmed. Meadow BCL (under MeadowAssembliesPath) is excluded. -->
      <TrimmerRootAssembly Include=""@(ManagedAssemblyToLink->'%(FileName)')""
          Condition=""!Exists('$(MeadowAssembliesPath)/%(FileName)%(Extension)')"" />

      <!-- If a Mono ILLink descriptor is provided alongside the BCL, use it to
           selectively preserve only the CoreLib types that the native Mono runtime
           loads by name (domain.c, mini-exceptions.c, etc.). This allows the trimmer
           to strip unused CoreLib code and dramatically reduce its size.
           Fallback: if no descriptor exists, root the entire assembly (safe but large). -->
      <TrimmerRootDescriptors Include=""$(MeadowAssembliesPath)/ILLink.Descriptors.xml""
          Condition=""Exists('$(MeadowAssembliesPath)/ILLink.Descriptors.xml')"" />
      <TrimmerRootAssembly Include=""System.Private.CoreLib""
          Condition=""!Exists('$(MeadowAssembliesPath)/ILLink.Descriptors.xml')"" />
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

    private static string GetLatestNetCoreAppVersion()
    {
        // Returns the highest installed .NET SDK major version as "vN.0" (e.g. "v10.0").
        // Used as the TFM override for legacy projects that can't natively publish-trim.
        // Falls back to v10.0 if `dotnet --list-sdks` can't be parsed.
        try
        {
            using var proc = new Process();
            proc.StartInfo.FileName = "dotnet";
            proc.StartInfo.Arguments = "--list-sdks";
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            // Each line looks like "10.0.201 [/usr/local/share/dotnet/sdk]"
            var maxMajor = output
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim().Split(' ').FirstOrDefault())
                .Where(v => !string.IsNullOrEmpty(v))
                .Select(v => int.TryParse(v!.Split('.').FirstOrDefault(), out var major) ? major : 0)
                .DefaultIfEmpty(0)
                .Max();

            return maxMajor > 0 ? $"v{maxMajor}.0" : "v10.0";
        }
        catch
        {
            return "v10.0";
        }
    }

    private static string? GetTargetFrameworkFromProject(string projectFilePath)
    {
        var projectFile = File.Exists(projectFilePath)
            ? projectFilePath
            : Directory.GetFiles(projectFilePath, "*.csproj").FirstOrDefault();

        if (projectFile == null) return null;

        var content = File.ReadAllText(projectFile);
        // Extract <TargetFramework>...</TargetFramework> value
        const string startTag = "<TargetFramework>";
        const string endTag = "</TargetFramework>";
        var start = content.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        start += startTag.Length;
        var end = content.IndexOf(endTag, start, StringComparison.OrdinalIgnoreCase);
        if (end < 0) return null;
        var tfm = content.Substring(start, end - start).Trim();
        return string.IsNullOrEmpty(tfm) ? null : tfm;
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