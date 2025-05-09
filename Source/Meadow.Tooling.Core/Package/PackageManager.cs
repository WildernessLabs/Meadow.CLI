﻿using GlobExpressions;
using Meadow.CLI;
using Meadow.Cloud.Client;
using Meadow.Software;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.Package;

public class PackageManager : BuildManager, IPackageManager
{
    public PackageManager(FileManager fileManager) : base(fileManager)
    {
    }

    public const string PackageMetadataFileName = "info.json";

    public Task<string> AssemblePackage(string contentSourceFolder,
        string outputFolder,
        string osVersion,
        string? mpakName = null,
        string filter = "**/*",
        bool overwrite = false,
        CancellationToken? cancellationToken = null)
    {
        var di = new DirectoryInfo(outputFolder);
        if (!di.Exists)
        {
            di.Create();
        }

        // uncomment to force ".mpak" extension. mpakName = Path.ChangeExtension(mpakName, ".mpak");
        mpakName = Path.Combine(outputFolder, string.IsNullOrWhiteSpace(mpakName) ? $"{DateTime.UtcNow:yyyyMMddff}.mpak" : mpakName);

        if (File.Exists(mpakName))
        {
            if (!overwrite)
            {
                throw new Exception($"Output file '{Path.GetFileName(mpakName)}' already exists.");
            }

            File.Delete(mpakName);
        }

        var appFiles = Glob.Files(contentSourceFolder, filter, GlobOptions.CaseInsensitive).ToArray();

        using var archive = ZipFile.Open(mpakName, ZipArchiveMode.Create);

        foreach (var fPath in appFiles)
        {
            var destination = Path.Combine("app", fPath);
            CreateEntry(archive, Path.Combine(contentSourceFolder, fPath), destination);
        }

        // write a metadata file info.json in the mpak
        // TODO: we need to see what is necessary and meaningful here and pass it in via param (or the entire file via param?)
        PackageInfo info = new()
        {
            Version = "1.0",
            OsVersion = osVersion
        };

        var infoJson = JsonSerializer.Serialize(info);
        File.WriteAllText(PackageMetadataFileName, infoJson);
        CreateEntry(archive, PackageMetadataFileName, Path.GetFileName(PackageMetadataFileName));

        return Task.FromResult(mpakName);
    }

    private void CreateEntry(ZipArchive archive, string fromFile, string entryPath)
    {
        // Windows '\' Path separator character will be written to the zip which meadow os does not properly unpack
        //  See: https://github.com/dotnet/runtime/issues/41914
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            entryPath = entryPath.Replace('\\', '/');
        }

        archive.CreateEntryFromFile(fromFile, entryPath);
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
}