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
            var timeOutTask = Task.Delay(timeoutInMs, cancellationToken);

            var tcs = new TaskCompletionSource<bool>();
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

                if (e.MessageType == MeadowMessageType.Concluded)
                {
                    tcs.TrySetResult(true);
                }
            };

            DataProcessor.OnReceiveData += handler;

            await ProcessCommand(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_LIST_PART_FILES_AND_CRC,
                userData: (uint) partition,
                timeoutMs: 30000,
                cancellationToken: cancellationToken);

            await Task.WhenAny(new Task[] {timeOutTask, tcs.Task});
            DataProcessor.OnReceiveData -= handler;

            return FilesOnDevice;
        }

        public override async Task<bool> WriteFileAsync(string filename,
                                                   string path,
                                                   int timeoutInMs = 200000,
                                                   CancellationToken cancellationToken = default)
        {
            if (IsDeviceInitialized() == false)
            {
                throw new Exception("Device is not initialized");
            }

            bool result = false;

            var timeOutTask = Task.Delay(timeoutInMs, cancellationToken);

            var tcs = new TaskCompletionSource<bool>();

            EventHandler<MeadowMessageEventArgs> handler = (s, e) =>
            {
                if (e.MessageType == MeadowMessageType.Concluded)
                {
                    result = true;
                    tcs.SetResult(true);
                }
            };

            DataProcessor.OnReceiveData += handler;

            await WriteFileInternal(
                filename,
                path,
                timeout: timeoutInMs,
                cancellationToken: cancellationToken);

            await Task.WhenAny(new Task[] {timeOutTask, tcs.Task});

            DataProcessor.OnReceiveData -= handler;

            return result;
        }

        public override async Task DeleteFileAsync(string fileName,
                                              int partition = 0,
                                              CancellationToken cancellationToken = default)
        {
            await TransmitFileInfoToExtFlash(
                    HcomMeadowRequestType.HCOM_MDOW_REQUEST_DELETE_FILE_BY_NAME,
                    fileName,
                    fileName,
                    partition,
                    0,
                    true,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await WaitForResponseMessage(
                    x => x.MessageType == MeadowMessageType.Concluded,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        public override Task EraseFlashAsync(CancellationToken cancellationToken = default)
        {
            return ProcessCommand(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_BULK_FLASH_ERASE,
                MeadowMessageType.SerialReconnect,
                timeoutMs: 200000,
                cancellationToken: cancellationToken);
        }

        public override Task VerifyErasedFlashAsync(CancellationToken cancellationToken = default)
        {
            return ProcessCommand(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_VERIFY_ERASED_FLASH,
                MeadowMessageType.SerialReconnect,
                timeoutMs: 200000,
                cancellationToken: cancellationToken);
        }

        public override Task PartitionFileSystemAsync(int numberOfPartitions = 2,
                                                 CancellationToken cancellationToken = default)
        {
            if (numberOfPartitions < 1 || numberOfPartitions > 8)
            {
                throw new IndexOutOfRangeException(
                    "Number of partitions must be between 1 & 8 inclusive");
            }

            return ProcessCommand(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_PARTITION_FLASH_FS,
                userData: (uint) numberOfPartitions,
                cancellationToken: cancellationToken);
        }

        public override Task MountFileSystemAsync(int partition = 0,
                                             CancellationToken cancellationToken = default)
        {
            return ProcessCommand(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_MOUNT_FLASH_FS,
                userData: (uint) partition,
                cancellationToken: cancellationToken);
        }

        public override Task InitializeFileSystemAsync(int partition = 0,
                                                  CancellationToken cancellationToken = default)
        {
            return ProcessCommand(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_INITIALIZE_FLASH_FS,
                userData: (uint) partition,
                cancellationToken: cancellationToken);
        }

        public override Task CreateFileSystemAsync(int partition = 0,
                                              CancellationToken cancellationToken = default)
        {
            return ProcessCommand(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_CREATE_ENTIRE_FLASH_FS,
                cancellationToken: cancellationToken);
        }

        public override Task FormatFileSystemAsync(int partition = 0,
                                              CancellationToken cancellationToken = default)
        {
            return ProcessCommand(
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_FORMAT_FLASH_FILE_SYS,
                userData: (uint) partition,
                cancellationToken: cancellationToken);
        }

        public override Task RenewFileSystemAsync(CancellationToken cancellationToken = default)
        {
            return ProcessCommand(HcomMeadowRequestType.HCOM_MDOW_REQUEST_PART_RENEW_FILE_SYS, MeadowMessageType.SerialReconnect, cancellationToken: cancellationToken);
        }


        public override async Task UpdateMonoRuntimeAsync(string fileName,
                                                     string? targetFileName = null,
                                                     int partition = 0,
                                                     CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Waiting for Meadow to be ready.");
            await WaitForReadyAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await MonoDisableAsync(cancellationToken)
                .ConfigureAwait(false);

            Trace.Assert(
                await GetMonoRunStateAsync(cancellationToken)
                            .ConfigureAwait(false),
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

            if (string.IsNullOrWhiteSpace(targetFileName))
            {
                targetFileName = Path.GetFileName(sourceFilename);
            }

            await TransmitFileInfoToExtFlash(
                    HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_UPDATE_RUNTIME,
                    sourceFilename,
                    targetFileName!,
                    partition,
                    0,
                    false,
                    true,
                    cancellationToken)
                .ConfigureAwait(false);

            await WaitForResponseMessage(
                    x => x.MessageType == MeadowMessageType.Concluded,
                    300000,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        public override async Task WriteFileToEspFlashAsync(string fileName,
                                                       string? targetFileName = null,
                                                       int partition = 0,
                                                       string? mcuDestAddr = null,
                                                       CancellationToken cancellationToken =
                                                           default)
        {
            // For the ESP32 on the meadow, we don't need the target file name, we just need the
            // MCU's destination address and the file's binary.
            // Assume if no mcuDestAddr that the fileName is a CSV with both file names and Mcu Addr
            if (mcuDestAddr != null)
            {
                // Since the mcuDestAddr is used we'll assume the fileName field just contains
                // a single file.
                if (string.IsNullOrWhiteSpace(targetFileName))
                {
                    // While not used by the ESP32 it cost nothing to send it and can help
                    // with debugging
                    targetFileName = Path.GetFileName(fileName);
                }

                // Convert mcuDestAddr from a string to a 32-bit unsigned int, but first
                // insure it starts with 0x
                UInt32 mcuAddr = 0;
                if (mcuDestAddr.StartsWith("0x") || mcuDestAddr.StartsWith("0X"))
                {
                    mcuAddr = UInt32.Parse(
                        mcuDestAddr.Substring(2),
                        System.Globalization.NumberStyles.HexNumber);
                }
                else
                {
                    Console.WriteLine(
                        $"The '--McuDestAddr' argument must be followed with an address in the form '0x1800'");

                    return;
                }

                await TransmitFileInfoToExtFlash(
                        HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER,
                        fileName,
                        targetFileName!,
                        partition,
                        mcuAddr,
                        false,
                        true,
                        cancellationToken)
                    .ConfigureAwait(false);

                await WaitForResponseMessage(
                        x => x.MessageType == MeadowMessageType.Concluded,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
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

                UInt32 mcuAddr;
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
                        // Fill in the Mcu Addr
                        mcuAddr = UInt32.Parse(
                            fileElement[i]
                                .Substring(2),
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
                    await TransmitFileInfoToExtFlash(
                            HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER,
                            fileElement[i + 1],
                            targetFileName,
                            partition,
                            mcuAddr,
                            false,
                            lastFile,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        public override async Task FlashEspAsync(string sourcePath,
                                            CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"Transferring {DownloadManager.NetworkMeadowCommsFilename}");
            await WriteFileToEspFlashAsync(
                    Path.Combine(sourcePath, DownloadManager.NetworkMeadowCommsFilename),
                    mcuDestAddr: "0x10000",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);

            Console.WriteLine($"Transferring {DownloadManager.NetworkBootloaderFilename}");
            await WriteFileToEspFlashAsync(
                    Path.Combine(sourcePath, DownloadManager.NetworkBootloaderFilename),
                    mcuDestAddr: "0x1000",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);

            Console.WriteLine($"Transferring {DownloadManager.NetworkPartitionTableFilename}");
            await WriteFileToEspFlashAsync(
                    Path.Combine(sourcePath, DownloadManager.NetworkPartitionTableFilename),
                    mcuDestAddr: "0x8000",
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);
        }

        public override Task ForwardVisualStudioDataToMonoAsync(byte[] debuggerData,
                                                           int userData,
                                                           CancellationToken cancellationToken = default)
        {
            return _sendTargetData.BuildAndSendSimpleData(
                debuggerData,
                HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEBUGGING_DEBUGGER_DATA,
                (uint) userData);
        }

        public override async Task DeployAppAsync(string applicationFilePath, CancellationToken cancellationToken = default)
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

            var deviceFiles = await GetFilesAndCrcsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            var extensions = new List<string> { ".exe", ".bmp", ".jpg", ".jpeg", ".json", ".xml", ".yml", ".txt" };

            var paths = Directory.EnumerateFiles(fi.DirectoryName, "*.*", SearchOption.TopDirectoryOnly)
            .Where(s => extensions.Contains(new FileInfo(s).Extension));

            var files = new List<string>();
            var crcs = new List<uint>();

            foreach (var file in paths)
            {
                using FileStream fs = File.Open(file, FileMode.Open);
                var len = (int)fs.Length;
                var bytes = new byte[len];

                await fs.ReadAsync(bytes, 0, len, cancellationToken);

                //0x
                var crc = CrcTools.Crc32part(bytes, len, 0); // 0x04C11DB7);

                //Console.WriteLine($"{file} crc is {crc}");
                files.Add(Path.GetFileName(file));
                crcs.Add(crc);
            }

            var dependencies = AssemblyManager.GetDependencies(fi.Name, fi.DirectoryName);

            //crawl dependencies
            foreach (var file in dependencies)
            {
                using FileStream fs = File.Open(Path.Combine(fi.DirectoryName, file), FileMode.Open);
                var len = (int)fs.Length;
                var bytes = new byte[len];

                await fs.ReadAsync(bytes, 0, len, cancellationToken);

                //0x
                var crc = CrcTools.Crc32part(bytes, len, 0); // 0x04C11DB7);

                Logger.LogInformation("{file} crc is {checksum}", file, crc);
                files.Add(Path.GetFileName(file));
                crcs.Add(crc);
            }

            // delete unused files
            foreach (var file in deviceFiles.Keys)
            {
                if (files.Contains(file) == false)
                {
                    await DeleteFileAsync(file, cancellationToken: cancellationToken).ConfigureAwait(false);
                    Logger.LogInformation("Removing file: {file}", file);
                }
            }

            // write new files
            for (int i = 0; i < files.Count; i++)
            {
                if (deviceFiles.Values.Contains(crcs[i]))
                {
                    Logger.LogInformation("Skipping file: {file}", files[i]);
                    continue;
                }

                if (!File.Exists(Path.Combine(fi.DirectoryName, files[i])))
                {
                    Logger.LogInformation("{file} not found", files[i]);
                    continue;
                }

                await WriteFileAsync(files[i], fi.DirectoryName, cancellationToken: cancellationToken).ConfigureAwait(false);
                Logger.LogInformation("Writing file: {file}", files[i]);
            }

            Logger.LogInformation("{file} deploy complete", fi.Name);
        }

        private protected void CopySystemNetHttpDll(string targetDir)
        {
            try
            {
                var bclNugetPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", "wildernesslabs.meadow.assemblies");

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

                        var sourcePath = Path.Combine(bclNugetPath, versions.Last().ToString(), "lib", "net472");
                        if (Directory.Exists(sourcePath))
                        {
                            if (File.Exists(Path.Combine(sourcePath, SystemHttpNetDllName)))
                            {
                                File.Copy(Path.Combine(sourcePath, SystemHttpNetDllName), Path.Combine(targetDir, SystemHttpNetDllName));
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

        private protected async Task<bool> WriteFileInternal(
            string filename,
            string? targetFileName = null,
            int partition = 0,
            int timeout = 200000,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(targetFileName))
            {
                targetFileName = Path.GetFileName(filename);
            }

            // For the STM32F7 on meadow, we need source file and destination file names.
            string[] csvArray = filename.Split(',');
            if (csvArray.Length == 1)
            {
                await TransmitFileInfoToExtFlash(
                        HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_FILE_TRANSFER,
                        filename,
                        targetFileName!,
                        partition,
                        0,
                        false,
                        true,
                        cancellationToken)
                    .ConfigureAwait(false);

                await WaitForResponseMessage(
                        x => x.MessageType == MeadowMessageType.Concluded,
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                // No CSV, just the source file name. So we'll assume the targetFileName is correct
                //TransmitFileInfoToExtFlash(meadow, meadowRequestType, fileName, targetFileName, partition, 0, false, true);
                return true;
            }
            else
            {
                // At this point, the fileName field should contain a CSV string containing the source
                // and destination file names, always in an even number.
                if (csvArray.Length % 2 != 0)
                {
                    Console.WriteLine(
                        "Please provide a CSV input with file names \"source, destination, source, destination\"");

                    return false;
                }

                for (int i = 0; i < csvArray.Length; i += 2)
                {
                    // Send files one-by-one
                    bool lastFile = i == csvArray.Length - 2;
                    await TransmitFileInfoToExtFlash(
                            HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_FILE_TRANSFER,
                            csvArray[i]
                                .Trim(),
                            csvArray[i + 1]
                                .Trim(),
                            partition,
                            0,
                            false,
                            lastFile,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            return false;
        }

        private async Task<FileTransferResult> TransmitFileInfoToExtFlash(
            HcomMeadowRequestType requestType,
            string sourceFileName,
            string targetFileName,
            int partition,
            uint mcuAddr,
            bool deleteFile,
            bool lastInSeries = false,
            CancellationToken cancellationToken = default)
        {
            var sw = new Stopwatch();
            try
            {
                if (string.IsNullOrWhiteSpace(sourceFileName))
                {
                    throw new ArgumentNullException(sourceFileName);
                }

                //----------------------------------------------
                if (deleteFile == true)
                {
                    // No data packets, no end-of-file message and no mcu address
                    await _sendTargetData.BuildAndSendFileRelatedCommand(
                                             requestType,
                                             (UInt32) partition,
                                             0,
                                             0,
                                             0,
                                             string.Empty,
                                             sourceFileName)
                                         .ConfigureAwait(false);

                    return FileTransferResult.EmptyResult;
                }

                var fi = new FileInfo(sourceFileName);
                if (!fi.Exists)
                {
                    throw new FileNotFoundException("Cannot find source file", fi.FullName);
                }

                // If ESP32 file we must also send the MD5 has of the file
                string md5Hash = string.Empty;
                if (mcuAddr != 0)
                {
                    using var md5 = MD5.Create();
                    using var stream = File.OpenRead(sourceFileName);
                    var hash = md5.ComputeHash(stream);
                    md5Hash = BitConverter.ToString(hash)
                                          .Replace("-", "")
                                          .ToLowerInvariant();
                }

                // Open, read and close the data file
                var fileBytes = File.ReadAllBytes(sourceFileName);
                var fileCrc32 = CrcTools.Crc32part(fileBytes, fileBytes.Length, 0);
                var fileLength = fileBytes.Length;

                sw.Start();
                sw.Restart();

                await _sendTargetData.SendTheEntireFile(
                                         requestType,
                                         targetFileName,
                                         (uint) partition,
                                         fileBytes,
                                         mcuAddr,
                                         fileCrc32,
                                         md5Hash,
                                         lastInSeries)
                                     .ConfigureAwait(false);

                sw.Stop();

                return new FileTransferResult(sw.ElapsedMilliseconds, fileLength, fileCrc32);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"TransmitFileInfoToExtFlash threw :{ex}");
                throw;
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