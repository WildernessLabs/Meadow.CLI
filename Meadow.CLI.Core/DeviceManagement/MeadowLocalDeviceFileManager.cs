using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.DeviceManagement.Tools;
using Meadow.CLI.Core.Internals.MeadowCommunication;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Core.DeviceManagement
{
    public abstract partial class MeadowLocalDevice
    {
        private protected static readonly string SystemHttpNetDllName = "System.Net.Http.dll";

        public override async Task<IDictionary<string, uint>> GetFilesAndCrcsAsync(
            int timeoutInMs = 10000,
            int partition = 0,
            CancellationToken cancellationToken = default)
        {
            var started = false;

            EventHandler<MeadowMessageEventArgs> handler = (s, e) =>
            {
                if (e.MessageType == MeadowMessageType.FileListTitle)
                {
                    FilesOnDevice.Clear();
                    started = true;
                }
                else if (started == false)
                {
                    //ignore everything until we've started a new file list request
                    return;
                }

                if (e.MessageType == MeadowMessageType.FileListCrcMember)
                {
                    SetFileAndCrcsFromMessage(e.Message);
                }
            };

            var command = new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_LIST_PART_FILES_AND_CRC)
                          .WithResponseHandler(handler)
                          .WithUserData((uint)partition).Build();

            await SendCommandAsync(command, cancellationToken)
                .ConfigureAwait(false);

            return FilesOnDevice;
        }

        /// <summary>
        /// Write a file to the Meadow
        /// </summary>
        /// <param name="filename">The name of the file</param>
        /// <param name="path">The path to the file</param>
        /// <param name="timeoutInMs">The amount of time to wait to write the file</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to cancel the operation</param>
        /// <returns></returns>
        public override async Task<FileTransferResult> WriteFileAsync(string filename,
                                                        string path,
                                                        int timeoutInMs = 200000,
                                                        CancellationToken cancellationToken =
                                                            default)
        {
            if (IsDeviceInitialized() == false)
            {
                throw new Exception("Device is not initialized");
            }
            
            var sourceFileName = Path.Combine(path, filename);
            var fi = new FileInfo(sourceFileName);
            if (!fi.Exists)
            {
                throw new FileNotFoundException("Cannot find source file", fi.FullName);
            }

            // If ESP32 file we must also send the MD5 has of the file
            using var md5 = MD5.Create();
            var fileBytes = await File.ReadAllBytesAsync(sourceFileName, cancellationToken);
            var hash = md5.ComputeHash(fileBytes);
            string md5Hash = BitConverter.ToString(hash)
                                         .Replace("-", "")
                                         .ToLowerInvariant();
            var fileCrc32 = CrcTools.Crc32part(fileBytes, fileBytes.Length, 0);

            var command = await new FileCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_FILE_TRANSFER)
                          .WithSourceFileName(sourceFileName)
                          .WithDestinationFileName(filename)
                          .WithTimeout(TimeSpan.FromMilliseconds(timeoutInMs))
                          .WithPartition(0)
                          .BuildAsync();

            var sw = Stopwatch.StartNew();
            await SendTheEntireFile(command, true, cancellationToken)
                .ConfigureAwait(false);
            sw.Stop();
            return new FileTransferResult(sw.ElapsedMilliseconds, fileBytes.Length, fileCrc32);
        }

        public override async Task DeleteFileAsync(string fileName,
                                                   uint partition = 0,
                                                   CancellationToken cancellationToken = default)
        {
            var command =
                await new FileCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_DELETE_FILE_BY_NAME)
                    .WithDestinationFileName(fileName)
                    .WithPartition(partition)
                    .WithResponseType(MeadowMessageType.Concluded)
                    .WithCompletionResponseType(MeadowMessageType.Concluded)
                    .BuildAsync();

            await SendCommandAsync(command, cancellationToken)
                .ConfigureAwait(false);
        }

        public override Task EraseFlashAsync(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_BULK_FLASH_ERASE)
                    .WithCompletionResponseType(MeadowMessageType.SerialReconnect).WithTimeout(TimeSpan.FromMinutes(5)).Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public override Task VerifyErasedFlashAsync(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_VERIFY_ERASED_FLASH)
                    .WithCompletionResponseType(MeadowMessageType.SerialReconnect).WithTimeout(TimeSpan.FromMinutes(5)).Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public override Task FormatFileSystemAsync(uint partition = 0,
                                                   CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_FORMAT_FLASH_FILE_SYS)
                    .WithCompletionResponseType(MeadowMessageType.SerialReconnect).WithTimeout(TimeSpan.FromMinutes(5)).WithUserData(partition).Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public override Task RenewFileSystemAsync(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_PART_RENEW_FILE_SYS)
                    .WithCompletionResponseType(MeadowMessageType.SerialReconnect).WithTimeout(TimeSpan.FromMinutes(5)).Build();

            return SendCommandAsync(command, cancellationToken);
        }


        public override async Task UpdateMonoRuntimeAsync(string fileName,
                                                          uint partition = 0,
                                                          CancellationToken cancellationToken =
                                                              default)
        {
            Logger.LogInformation("Starting Mono Runtime Update");
            Logger.LogInformation("Waiting for Meadow to be ready");
            await WaitForReadyAsync(DefaultTimeout, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            Logger.LogDebug("Calling Mono Disable");
            await MonoDisableAsync(cancellationToken)
                .ConfigureAwait(false);

            Trace.Assert(
                await GetMonoRunStateAsync(cancellationToken)
                    .ConfigureAwait(false)
             == false,
                "Meadow was expected to have Mono Disabled");

            Logger.LogInformation("Updating Mono Runtime");

            var sourceFilename = fileName;
            if (string.IsNullOrWhiteSpace(sourceFilename))
            {
                // check local override
                sourceFilename = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    DownloadManager.RuntimeFilename);

                if (File.Exists(sourceFilename))
                {
                    Logger.LogInformation(
                        $"Using current directory '{DownloadManager.RuntimeFilename}'");
                }
                else
                {
                    sourceFilename = Path.Combine(
                        DownloadManager.FirmwareDownloadsFilePath,
                        DownloadManager.RuntimeFilename);

                    if (File.Exists(sourceFilename))
                    {
                        Logger.LogInformation("FileName not specified, using latest download.");
                    }
                    else
                    {
                        Logger.LogInformation(
                            "Unable to locate a runtime file. Either provide a path or download one.");

                        return; // KeepConsoleOpen?
                    }
                }
            }

            if (!File.Exists(sourceFilename))
            {
                Logger.LogInformation($"File '{sourceFilename}' not found");
                return;
            }

            var targetFileName = Path.GetFileName(sourceFilename);

            Logger.LogDebug("Sending Mono Update Runtime Request");
            var command = await new FileCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_UPDATE_RUNTIME)
                                .WithPartition(partition)
                                .WithDestinationFileName(targetFileName)
                                .WithSourceFileName(sourceFilename)
                                .BuildAsync()
                                .ConfigureAwait(false);

            await SendTheEntireFile(command, true, cancellationToken).ConfigureAwait(false);
        }

        public override async Task WriteFileToEspFlashAsync(string fileName,
                                                            uint partition = 0,
                                                            string? mcuDestAddress = null,
                                                            CancellationToken cancellationToken =
                                                                default)
        {
            var targetFileName = Path.GetFileName(fileName);
            // For the ESP32 on the meadow, we don't need the target file name, we just need the
            // MCU's destination address and the file's binary.
            // Assume if no mcuDestAddress that the fileName is a CSV with both file names and Mcu Address
            if (mcuDestAddress != null)
            {
                // Convert mcuDestAddress from a string to a 32-bit unsigned int, but first
                // insure it starts with 0x
                uint mcuAddress = 0;
                if (mcuDestAddress.StartsWith("0x") || mcuDestAddress.StartsWith("0X"))
                {
                    mcuAddress = uint.Parse(
                        mcuDestAddress.Substring(2),
                        System.Globalization.NumberStyles.HexNumber);
                }
                else
                {
                    Console.WriteLine(
                        $"The '--McuDestAddress' argument must be followed with an address in the form '0x1800'");

                    return;
                }
                var command = await new FileCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER)
                                    .WithPartition(partition)
                                    .WithDestinationFileName(targetFileName)
                                    .WithMcuAddress(mcuAddress)
                                    .WithSourceFileName(fileName)
                                    .BuildAsync()
                                    .ConfigureAwait(false);

                await SendTheEntireFile(command, true, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // At this point, the fileName field should contain a CSV string containing the destination
                // addresses followed by file's location within the host's file system.
                // E.g. "0x8000, C:\Blink\partition-table.bin, 0x1000, C:\Blink\bootloader.bin, 0x10000, C:\Blink\blink.bin"
                string[] fileElement = fileName.Split(',');
                if (fileElement.Length % 2 != 0)
                {
                    Console.WriteLine(
                        "Please provide a CSV input with \"address, fileName, address, fileName\"");

                    return;
                }

                uint mcuAddress;
                for (int i = 0; i < fileElement.Length; i += 2)
                {
                    // Trim any white space from this mcu addr and file name
                    fileElement[i] = fileElement[i]
                        .Trim();

                    fileElement[i + 1] = fileElement[i + 1]
                        .Trim();

                    if (fileElement[i]
                            .StartsWith("0x")
                     || fileElement[i]
                            .StartsWith("0X"))
                    {
                        // Fill in the Mcu Address
                        mcuAddress = uint.Parse(fileElement[i][2..],
                                             System.Globalization.NumberStyles.HexNumber);
                    }
                    else
                    {
                        Console.WriteLine("Please provide a CSV input with addresses like 0x1234");
                        return;
                    }

                    // Meadow.CLI --Esp32WriteFile --SerialPort Com26 --File
                    // "0x8000, C:\Download\Esp32\Hello\partition-table.bin, 0x1000, C:\Download\Esp32\Hello\bootloader.bin, 0x10000, C:\Download\Esp32\Hello\hello-world.bin"
                    // File Path and Name
                    targetFileName = Path.GetFileName(fileElement[i + 1]);
                    bool lastFile = i == fileElement.Length - 2;

                    // this may need need to be awaited?
                    var command = await new FileCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER)
                                  .WithPartition(partition)
                                  .WithDestinationFileName(targetFileName)
                                  .WithMcuAddress(mcuAddress)
                                  .WithSourceFileName(fileElement[i+1])
                                  .BuildAsync()
                                  .ConfigureAwait(false);

                    await SendTheEntireFile(command, lastFile, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        public override async Task FlashEspAsync(string sourcePath,
                                                 CancellationToken cancellationToken = default)
        {
            Logger.LogInformation($"Transferring {DownloadManager.NetworkMeadowCommsFilename}");
            await WriteFileToEspFlashAsync(
                    Path.Combine(sourcePath, DownloadManager.NetworkMeadowCommsFilename),
                    mcuDestAddress: "0x10000",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);

            Logger.LogInformation($"Transferring {DownloadManager.NetworkBootloaderFilename}");
            await WriteFileToEspFlashAsync(
                    Path.Combine(sourcePath, DownloadManager.NetworkBootloaderFilename),
                    mcuDestAddress: "0x1000",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);

            Logger.LogInformation($"Transferring {DownloadManager.NetworkPartitionTableFilename}");
            await WriteFileToEspFlashAsync(
                    Path.Combine(sourcePath, DownloadManager.NetworkPartitionTableFilename),
                    mcuDestAddress: "0x8000",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);
        }

        public override async Task<string?> GetInitialBytesFromFile(
            string fileName,
            uint partition = 0,
            CancellationToken cancellationToken = default)
        {
            Logger.LogDebug("Getting initial bytes from {fileName}", fileName);
            var encodedFileName = System.Text.Encoding.UTF8.GetBytes(fileName);

            var command = new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_GET_INITIAL_FILE_BYTES)
                          .WithResponseType(MeadowMessageType.InitialFileData)
                          .WithData(encodedFileName)
                          .Build();

            var commandResponse = await SendCommandAsync(command, cancellationToken);

            if (!commandResponse.IsSuccess)
            {
                Logger.LogWarning("No bytes found for file.");
            }

            return commandResponse.Message;
        }

        public override Task ForwardVisualStudioDataToMonoAsync(byte[] debuggerData,
                                                                uint userData,
                                                                CancellationToken cancellationToken = default)
        {
            var command = new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEBUGGING_DEBUGGER_DATA)
                          .WithData(debuggerData)
                          .WithResponseType(MeadowMessageType.Accepted)
                          .WithCompletionResponseType(MeadowMessageType.Accepted)
                          .WithUserData(userData)
                          .WithAcknowledgement(false)
                          .Build();

            return SendCommandAsync(command, cancellationToken);
        }

        public override async Task DeployAppAsync(string applicationFilePath,
                                                  bool includePdbs = false,
                                                  CancellationToken cancellationToken = default)
        {
            if (!File.Exists(applicationFilePath))
            {
                Console.WriteLine($"{applicationFilePath} not found.");
                return;
            }

            var fi = new FileInfo(applicationFilePath);
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                // for some strange reason, System.Net.Http.dll doesn't get copied to the output folder in VS.
                // so, we need to copy it over from the meadow assemblies nuget.
                CopySystemNetHttpDll(fi.DirectoryName);
            }

            var deviceFiles = await GetFilesAndCrcsAsync(cancellationToken: cancellationToken)
                                  .ConfigureAwait(false);

            foreach (var (filename, crc) in deviceFiles)
            {
                Logger.LogInformation("Found {file} (CRC: {crc})", filename, crc);
            }

            var extensions = new List<string>
                             { ".exe", ".bmp", ".jpg", ".jpeg", ".json", ".xml", ".yml", ".txt" };

            var paths = Directory.EnumerateFiles(
                                     fi.DirectoryName,
                                     "*.*",
                                     SearchOption.TopDirectoryOnly)
                                 .Where(s => extensions.Contains(new FileInfo(s).Extension));

            var files = new Dictionary<string, uint>();

            async Task AddFile(string file, bool includePdbs)
            {
                await using FileStream fs = File.Open(file, FileMode.Open);
                var len = (int)fs.Length;
                var bytes = new byte[len];

                await fs.ReadAsync(bytes, 0, len, cancellationToken);

                //0x
                var crc = CrcTools.Crc32part(bytes, len, 0); // 0x04C11DB7);

                Logger.LogDebug("{file} crc is {crc:X8}", file, crc);
                files.Add(Path.GetFileName(file), crc);
                if (includePdbs)
                {
                    var pdbFile = Path.ChangeExtension(file, "pdb");
                    if (File.Exists(pdbFile))
                        await AddFile(pdbFile, false)
                            .ConfigureAwait(false);
                }
            }

            foreach (var file in paths)
            {
                await AddFile(file, includePdbs)
                    .ConfigureAwait(false);
            }

            var dependencies = AssemblyManager.GetDependencies(fi.Name, fi.DirectoryName);

            //crawl dependencies
            foreach (var file in dependencies)
            {
                await AddFile(Path.Combine(fi.DirectoryName, file), includePdbs);
            }

            // delete unused files
            foreach (var file in deviceFiles.Keys)
            {
                if (files.ContainsKey(file) == false)
                {
                    await DeleteFileAsync(file, cancellationToken: cancellationToken)
                        .ConfigureAwait(false);

                    Logger.LogInformation("Removing file: {file}", file);
                }
            }

            // write new files
            foreach(var file in files)
            {
                if (deviceFiles.ContainsKey(file.Key) && deviceFiles[file.Key] == file.Value)
                {
                    Logger.LogInformation("Skipping file (hash match): {file}", file.Key);
                    continue;
                }

                if (!File.Exists(Path.Combine(fi.DirectoryName, file.Key)))
                {
                    Logger.LogInformation("{file} not found", file.Key);
                    continue;
                }

                Logger.LogInformation("Writing file: {file}", file.Key);
                await WriteFileAsync(
                        file.Key,
                        fi.DirectoryName,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                Logger.LogInformation("Wrote file: {file}", file.Key);
            }

            Logger.LogInformation("{file} deploy complete", fi.Name);
        }

        private protected void CopySystemNetHttpDll(string targetDir)
        {
            try
            {
                var bclNugetPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".nuget",
                    "packages",
                    "wildernesslabs.meadow.assemblies");

                if (Directory.Exists(bclNugetPath))
                {
                    List<Version> versions = new List<Version>();

                    var versionFolders = Directory.EnumerateDirectories(bclNugetPath);
                    foreach (var versionFolder in versionFolders)
                    {
                        var di = new DirectoryInfo(versionFolder);
                        if (Version.TryParse(di.Name, out Version outVersion))
                        {
                            versions.Add(outVersion);
                        }
                    }

                    if (versions.Any())
                    {
                        versions.Sort();

                        var sourcePath = Path.Combine(
                            bclNugetPath,
                            versions.Last()
                                    .ToString(),
                            "lib",
                            "net472");

                        if (Directory.Exists(sourcePath))
                        {
                            if (File.Exists(Path.Combine(sourcePath, SystemHttpNetDllName)))
                            {
                                File.Copy(
                                    Path.Combine(sourcePath, SystemHttpNetDllName),
                                    Path.Combine(targetDir,  SystemHttpNetDllName));
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // eat this for now
            }
        }

        private void SetFileAndCrcsFromMessage(string fileListMember)
        {
            int fileNameStart = fileListMember.LastIndexOf('/') + 1;
            int crcStart = fileListMember.IndexOf('[') + 1;
            if (fileNameStart == 0 && crcStart == 0)
                return; // No files found

            Debug.Assert(crcStart > fileNameStart);

            var file = fileListMember.Substring(fileNameStart, crcStart - fileNameStart - 2);
            var crc = Convert.ToUInt32(fileListMember.Substring(crcStart, 10), 16);
            FilesOnDevice.Add(file.Trim(), crc);
        }
    }
}