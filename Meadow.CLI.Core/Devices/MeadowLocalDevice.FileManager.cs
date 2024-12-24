using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.DeviceManagement.Tools;
using Meadow.CLI.Core.Internals.MeadowCommunication;
using Meadow.Hcom;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace Meadow.CLI.Core.Devices
{
    public abstract partial class MeadowLocalDevice
    {
        private string[] dllLinkIngoreList = { "System.Threading.Tasks.Extensions.dll" };//, "Microsoft.Extensions.Primitives.dll" };
        private string[] pdbLinkIngoreList = { "System.Threading.Tasks.Extensions.pdb" };//, "Microsoft.Extensions.Primitives.pdb" };

        public async Task<IList<string>> GetFilesAndFolders(
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            var started = false;

            var items = new List<string>();

            EventHandler<MeadowMessageEventArgs> handler = (s, e) =>
            {
                if (e.MessageType == MeadowMessageType.Accepted)
                {
                    started = true;
                }
                else if (started == false)
                {   //ignore everything until we've started a new file list request
                    return;
                }

                if (e.MessageType == MeadowMessageType.Data)
                {
                    items.Add(e.Message);
                }
            };

            var command = new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_GET_FILES_AND_FOLDERS)
                    .WithResponseHandler(handler)
                    .Build();

            await SendCommand(command, cancellationToken);

            return items;
        }

        public async Task<IList<FileData>> GetFilesAndCrcs(
            TimeSpan timeout,
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
                    if (FileData.TryParse(e.Message, out FileData? data))
                    {
                        FilesOnDevice.Add(data);
                    }
                }
            };

            var command = new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_LIST_PART_FILES_AND_CRC)
                    .WithResponseHandler(handler)
                    .WithUserData((uint)partition)
                    .Build();

            await SendCommand(command, cancellationToken);

            return FilesOnDevice;
        }

        /// <summary>
        /// Write a file to the Meadow
        /// </summary>
        /// <param name="sourceFileName">The name of the file</param>
        /// <param name="destinationFileName">The path to the file</param>
        /// <param name="timeout">The amount of time to wait to write the file</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to cancel the operation</param>
        /// <returns></returns>
        public async Task<FileTransferResult> WriteFile(string sourceFileName,
                                                             string destinationFileName,
                                                             TimeSpan timeout,
                                                             CancellationToken cancellationToken =
                                                                 default)
        {
            if (IsDeviceInitialized() == false)
            {
                throw new Exception("Device is not initialized");
            }

            var fi = new FileInfo(sourceFileName);
            if (!fi.Exists)
            {
                throw new FileNotFoundException("Cannot find source file", fi.FullName);
            }

            // If ESP32 file we must also send the MD5 has of the file
            using var md5 = MD5.Create();

            byte[] fileBytes;
            using (var stream = File.Open(sourceFileName, FileMode.Open))
            {
                var streamLength = (int)stream.Length;
                fileBytes = new byte[streamLength];
                var bytesRead = 0;

                while (stream.Position < streamLength)
                {
                    bytesRead += await stream.ReadAsync(fileBytes, bytesRead, streamLength, cancellationToken);
                }

                if (bytesRead != streamLength)
                {
                    throw new InvalidDataException($"Read bytes: {bytesRead} from {sourceFileName} does not match stream Length: {streamLength}!");
                }
            }

            var hash = md5.ComputeHash(fileBytes);
            string md5Hash = BitConverter.ToString(hash)
                                         .Replace("-", "")
                                         .ToLowerInvariant();

            var fileCrc32 = CrcTools.Crc32part(fileBytes, fileBytes.Length, 0);

            var command =
                new FileCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_FILE_TRANSFER)
                    .WithSourceFileName(sourceFileName)
                    .WithDestinationFileName(destinationFileName)
                    .WithTimeout(timeout)
                    .WithPartition(0)
                    .Build();

            var sw = Stopwatch.StartNew();

            await SendTheEntireFile(command, true, cancellationToken);

            sw.Stop();

            return new FileTransferResult(sw.ElapsedMilliseconds, fileBytes.Length, fileCrc32);
        }

        public async Task DeleteFile(string fileName,
                                          uint partition = 0,
                                          CancellationToken cancellationToken = default)
        {
            var command =
                 new FileCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_DELETE_FILE_BY_NAME)
                    .WithDestinationFileName(fileName)
                    .WithPartition(partition)
                    .WithResponseType(MeadowMessageType.Concluded)
                    .WithCompletionResponseType(MeadowMessageType.Concluded)
                    .Build();

            await SendCommand(command, cancellationToken);
        }

        public Task EraseFlash(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_BULK_FLASH_ERASE)
                    .WithCompletionResponseType(MeadowMessageType.SerialReconnect)
                    .WithTimeout(TimeSpan.FromMinutes(5))
                    .Build();

            return SendCommand(command, cancellationToken);
        }

        public Task VerifyErasedFlash(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_VERIFY_ERASED_FLASH)
                    .WithCompletionResponseType(MeadowMessageType.SerialReconnect)
                    .WithTimeout(TimeSpan.FromMinutes(5))
                    .Build();

            return SendCommand(command, cancellationToken);
        }

        public Task FormatFileSystem(uint partition = 0,
                                          CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_FORMAT_FLASH_FILE_SYS)
                    .WithCompletionResponseType(MeadowMessageType.SerialReconnect)
                    .WithTimeout(TimeSpan.FromMinutes(5))
                    .WithUserData(partition)
                    .Build();

            return SendCommand(command, cancellationToken);
        }

        public Task RenewFileSystem(CancellationToken cancellationToken = default)
        {
            var command =
                new SimpleCommandBuilder(
                        HcomMeadowRequestType.HCOM_MDOW_REQUEST_PART_RENEW_FILE_SYS)
                    .WithCompletionResponseType(MeadowMessageType.SerialReconnect)
                    .WithTimeout(TimeSpan.FromMinutes(5))
                    .Build();

            return SendCommand(command, cancellationToken);
        }

        public Task UpdateMonoRuntime(string? fileName,
                                      uint partition = 0,
                                      CancellationToken cancellationToken = default)
        {
            return UpdateMonoRuntime(fileName, null, partition, cancellationToken);
        }

        public async Task UpdateMonoRuntime(string? fileName,
                                            string? osVersion,
                                            uint partition = 0,
                                            CancellationToken cancellationToken = default)
        {
            var sourceFilename = fileName;

            if (string.IsNullOrWhiteSpace(sourceFilename))
            {
                if (string.IsNullOrWhiteSpace(osVersion) == false)
                {
                    sourceFilename = Path.Combine(
                                        DownloadManager.FirmwarePathForVersion(osVersion),
                                        DownloadManager.RuntimeFilename);
                }
                else
                {
                    sourceFilename = Path.Combine(
                                        DownloadManager.FirmwareDownloadsFilePath,
                                        DownloadManager.RuntimeFilename);
                }

                if (File.Exists(sourceFilename))
                {
                    Logger.LogInformation($"Writing {sourceFilename} runtime");
                }
                else
                {
                    Logger.LogInformation("Unable to locate a runtime file. Either provide a path or download one.");
                    return;
                }
            }
            else if (!File.Exists(sourceFilename))
            {
                sourceFilename = Path.Combine(Directory.GetCurrentDirectory(), sourceFilename);

                if (!File.Exists(sourceFilename))
                {
                    Logger.LogInformation($"File '{sourceFilename}' not found");
                    return;
                }
            }

            var targetFileName = Path.GetFileName(sourceFilename);

            Logger.LogDebug("Sending Mono Update Runtime Request");
            var command =
                new FileCommandBuilder(
                          HcomMeadowRequestType.HCOM_MDOW_REQUEST_MONO_UPDATE_RUNTIME)
                          .WithPartition(partition)
                          .WithDestinationFileName(targetFileName)
                          .WithSourceFileName(sourceFilename)
                          .Build();

            await SendTheEntireFile(command, true, cancellationToken);
        }

        public async Task WriteFileToEspFlash(string fileName,
                                                   uint partition = 0,
                                                   string? mcuDestAddress = null,
                                                   CancellationToken cancellationToken = default)
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
                    Logger.LogError(
                        $"The '--McuDestAddress' argument must be followed with an address in the form '0x1800'");

                    return;
                }

                var command =
                    new FileCommandBuilder(
                              HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER)
                              .WithPartition(partition)
                              .WithDestinationFileName(targetFileName)
                              .WithMcuAddress(mcuAddress)
                              .WithSourceFileName(fileName)
                              .WithTimeout(new TimeSpan(hours: 0, minutes: 3, seconds: 0))
                              .Build();

                await SendTheEntireFile(command, true, cancellationToken);
            }
            else
            {
                // At this point, the fileName field should contain a CSV string containing the destination
                // addresses followed by file's location within the host's file system.
                // E.g. "0x8000, C:\Blink\partition-table.bin, 0x1000, C:\Blink\bootloader.bin, 0x10000, C:\Blink\blink.bin"
                string[] fileElement = fileName.Split(',');
                if (fileElement.Length % 2 != 0)
                {
                    Logger.LogError(
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
                        mcuAddress = uint.Parse(
                            fileElement[i].Substring(2),
                            System.Globalization.NumberStyles.HexNumber);
                    }
                    else
                    {
                        Logger.LogError("Please provide a CSV input with addresses like 0x1234");
                        return;
                    }

                    // Meadow.CLI --Esp32WriteFile --SerialPort Com26 --File
                    // "0x8000, C:\Download\Esp32\Hello\partition-table.bin, 0x1000, C:\Download\Esp32\Hello\bootloader.bin, 0x10000, C:\Download\Esp32\Hello\hello-world.bin"
                    // File Path and Name
                    targetFileName = Path.GetFileName(fileElement[i + 1]);
                    bool lastFile = i == fileElement.Length - 2;

                    // this may need need to be awaited?
                    var command =
                        new FileCommandBuilder(
                                  HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_ESP_FILE_TRANSFER)
                                  .WithPartition(partition)
                                  .WithDestinationFileName(targetFileName)
                                  .WithMcuAddress(mcuAddress)
                                  .WithSourceFileName(fileElement[i + 1])
                                  .Build();

                    await SendTheEntireFile(command, lastFile, cancellationToken);
                }
            }
        }

        public async Task FlashEsp(string? sourcePath,
                                        string? osVersion = null,
                                        CancellationToken cancellationToken = default)
        {
            if (osVersion == null)
            {
                sourcePath ??= DownloadManager.FirmwareDownloadsFilePath;
            }
            else
            {
                sourcePath = DownloadManager.FirmwarePathForVersion(osVersion);
            }

            Logger.LogInformation($"Transferring {Path.Combine(sourcePath, DownloadManager.NetworkMeadowCommsFilename)}");
            await WriteFileToEspFlash(
                    Path.Combine(sourcePath, DownloadManager.NetworkMeadowCommsFilename),
                    mcuDestAddress: "0x10000",
                    cancellationToken: cancellationToken);

            await Task.Delay(3000, cancellationToken);

            Logger.LogInformation($"Transferring {Path.Combine(sourcePath, DownloadManager.NetworkBootloaderFilename)}");
            await WriteFileToEspFlash(
                    Path.Combine(sourcePath, DownloadManager.NetworkBootloaderFilename),
                    mcuDestAddress: "0x1000",
                    cancellationToken: cancellationToken);

            await Task.Delay(1000, cancellationToken);

            Logger.LogInformation($"Transferring {Path.Combine(sourcePath, DownloadManager.NetworkPartitionTableFilename)}");
            await WriteFileToEspFlash(
                    Path.Combine(sourcePath, DownloadManager.NetworkPartitionTableFilename),
                    mcuDestAddress: "0x8000",
                    cancellationToken: cancellationToken);

            await Task.Delay(1000, cancellationToken);
        }

        public async Task<string?> GetInitialBytesFromFile(string fileName,
                                                           uint partition = 0,
                                                           CancellationToken cancellationToken =
                                                               default)
        {
            Logger.LogDebug("Getting initial bytes from {fileName}", fileName);
            var encodedFileName = System.Text.Encoding.UTF8.GetBytes(fileName);

            var command =
                new SimpleCommandBuilder(
                        HcomMeadowRequestType.HCOM_MDOW_REQUEST_GET_INITIAL_FILE_BYTES)
                    .WithResponseType(MeadowMessageType.InitialFileData)
                    .WithData(encodedFileName)
                    .Build();

            var commandResponse = await SendCommand(command, cancellationToken);

            if (!commandResponse.IsSuccess)
            {
                Logger.LogWarning("No bytes found for file.");
            }

            return commandResponse.Message;
        }

        public Task ForwardVisualStudioDataToMono(byte[] debuggerData,
                                                       uint userData,
                                                       CancellationToken cancellationToken =
                                                           default)
        {
            var command =
                new SimpleCommandBuilder(HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEBUGGING_DEBUGGER_DATA)
                    .WithData(debuggerData)
                    .WithResponseType(MeadowMessageType.Accepted)
                    .WithCompletionResponseType(MeadowMessageType.Accepted)
                    .WithUserData(userData)
                    .WithAcknowledgement(false)
                    .Build();

            return SendCommand(command, cancellationToken);
        }

        //ToDo this is super fragile (extra-super fragile!)
        //Need updated API to read files after B5.1
        private async Task DeleteTemporaryFiles(CancellationToken cancellationToken = default)
        {
            var items = await GetFilesAndFolders(new TimeSpan(0, 0, 15), cancellationToken);

            bool isRoot = false;
            bool isFolder = false;
            string folderName = string.Empty;

            var filesToDelete = new List<string>();

            for (int i = 0; i < items.Count; i++)
            {   //start of folders in root
                if (isRoot == false && items[i].Contains("meadow0/"))
                {
                    isRoot = true;
                } //next root folder - break out
                else if (isRoot && items[i].Contains(" ") == false)
                {
                    break;
                } //item under meadow0
                else if (isRoot &&
                    items[i].Contains("/") &&
                    items[i].Contains("./") == false)
                {
                    folderName = items[i].Substring(1);
                    isFolder = true;
                }
                else if (isFolder == true &&
                    items[i].Contains("  ") &&
                    items[i].Contains("[file]"))
                {
                    var end = items[i].IndexOf(" [file]");
                    filesToDelete.Add(Path.Combine(folderName, items[i].Substring(2, end - 2)));
                }
                else
                {
                    continue;
                }
            }

            foreach (var item in filesToDelete)
            {
                if (item.Contains("Data/") ||
                    item.Contains("Documents/"))
                {
                    continue;
                }

                Logger.LogInformation($"Deleting {item}");
                await DeleteFile(item, 0, cancellationToken);
            }
        }

        private record BuildOptions
        {
            public DeployOptions Deploy { get; set; }

            public record DeployOptions
            {
                public List<string> NoLink { get; set; }
                public bool? IncludePDBs { get; set; }
            }
        }

        public async Task DeployApp(string applicationFilePath,
                                    string osVersion,
                                    bool includePdbs = false,
                                    bool verbose = false,
                                    IList<string>? noLink = null,
                                    CancellationToken cancellationToken = default)
        {
            try
            {
                // does a meadow.build.yml file exist?
                var buildOptionsFile = Path.Combine(Path.GetDirectoryName(applicationFilePath), "app.build.yaml");
                if (File.Exists(buildOptionsFile))
                {
                    Logger.LogInformation($"'app.build.yaml' found!");

                    var yaml = File.ReadAllText(buildOptionsFile);
                    var deserializer = new DeserializerBuilder()
                        .IgnoreUnmatchedProperties()
                        .Build();
                    var opts = deserializer.Deserialize<BuildOptions>(yaml);

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
            catch (Exception ex)
            {
                Logger.LogInformation($"Failed to read app.build.yaml: {ex.Message}");
            }

            try
            {
                if (!File.Exists(applicationFilePath))
                {
                    Logger.LogError($"{applicationFilePath} not found.");
                    return;
                }

                var fi = new FileInfo(applicationFilePath);
                var directoryName = fi.DirectoryName ?? string.Empty;
                var meadowDll = Path.Combine(directoryName, "Meadow.dll");

                if (!File.Exists(meadowDll))
                {
                    Logger.LogError($"{meadowDll} not found.");
                    return;
                }

                var fileName = fi.Name;
                var fileNamePdb = Path.Combine(directoryName, "App.pdb");

                await DeleteTemporaryFiles(cancellationToken);

                var deviceFiles = await GetFilesAndCrcs(
                                          DefaultTimeout,
                                          cancellationToken: cancellationToken);

                foreach (var f in deviceFiles)
                {
                    Logger.LogInformation($"Found {f.FileName} (CRC: {f.Crc})" + Environment.NewLine);
                }

                var binaries = Directory.EnumerateFiles(directoryName, "*.*", SearchOption.TopDirectoryOnly)
                                       .Where(s => new FileInfo(s).Extension != ".dll")
                                       .Where(s => new FileInfo(s).Extension != ".pdb")
                                       .Where(s => !s.Contains(".DS_Store"));
                //                 .Where(s => extensions.Contains(new FileInfo(s).Extension));

                var files = new Dictionary<string, uint>();

                // Add the App.dll
                await AddFile(Path.Combine(directoryName, fileName), false);

                if (includePdbs)
                {
                    await AddFile(fileNamePdb, false);
                }
                else if (verbose)
                {
                    Logger.LogInformation("Skipping PDBs");
                }


                async Task AddFile(string file, bool includePdbs)
                {
                    if (files.ContainsKey(Path.GetFileName(file)))
                    {
                        return;
                    }

                    using FileStream fs = File.Open(file, FileMode.Open);
                    var len = (int)fs.Length;
                    var bytes = new byte[len];

                    await fs.ReadAsync(bytes, 0, len, cancellationToken);

                    //0x
                    var crc = CrcTools.Crc32part(bytes, len, 0); // 0x04C11DB7);

                    Logger.LogDebug("{file} crc is {crc:X8}", file, crc);
                    if (files.ContainsKey(file))
                    {
                        // file already exists in our list?
                        // this feels like a bug, but for now we'll see if it has the same CRC
                        if (crc != files[file])
                        {
                            throw new Exception($"To differing (by CRC) versions of file '{file}' exist in the build output");
                        }

                        // same file CRC, just ignore the new call to add it
                    }
                    else
                    {
                        files.Add(file, crc);
                    }

                    if (includePdbs)
                    {
                        var pdbFile = Path.ChangeExtension(file, "pdb");
                        if (File.Exists(pdbFile))
                            await AddFile(pdbFile, false);
                    }
                }

                var dependencies = AssemblyManager.GetDependencies(fileName, directoryName, osVersion)
                                    .Except(new string[] { "App.dll", "App.exe" })
                                    .ToList();

                var trimmedDependencies = await AssemblyManager.TrimDependencies(fileName, directoryName, dependencies, noLink, Logger, includePdbs: includePdbs, verbose: verbose);

                //add local files (this includes App.exe)
                foreach (var file in binaries)
                {
                    await AddFile(file, false);
                }

                if (trimmedDependencies != null)
                {
                    trimmedDependencies = trimmedDependencies.Where(x => x.Contains("App.") == false)
                        .Where(x => dllLinkIngoreList.Any(f => x.Contains(f)) == false)
                        .Where(x => pdbLinkIngoreList.Any(f => x.Contains(f)) == false)
                        .ToList();

                    //crawl trimmed dependencies
                    foreach (var file in trimmedDependencies)
                    {
                        await AddFile(file, false);
                    }

                    for (int i = 0; i < dllLinkIngoreList.Length; i++)
                    {   //add the files from the dll link ignore list
                        if (dependencies.Exists(f => f.Contains(dllLinkIngoreList[i])))
                        {
                            await AddFile(dependencies.FirstOrDefault(f => f.Contains(dllLinkIngoreList[i])), includePdbs);
                        }
                    }
                }
                else
                {
                    //crawl dependencies
                    foreach (var file in dependencies)
                    {
                        await AddFile(file, false);
                    }
                }

                // delete unused files
                foreach (var devicefile in deviceFiles)
                {
                    bool found = false;
                    foreach (var localfile in files.Keys)
                    {
                        if (Path.GetFileName(localfile).Equals(devicefile.FileName))
                        {
                            found = true;
                        }
                        if (found) { break; }
                    }
                    if (found == false)
                    {
                        await DeleteFile(devicefile.FileName, cancellationToken: cancellationToken);

                        Logger.LogInformation($"Removing file: {devicefile}" + Environment.NewLine);
                    }
                }

                // write new files
                foreach (var file in files)
                {
                    // does the file name and CRC match?
                    var filename = Path.GetFileName(file.Key);

                    if (deviceFiles.Any(d => d.FileName == filename && d.Crc == file.Value))
                    {
                        Logger.LogInformation($"Skipping file (hash match): {filename}" + Environment.NewLine);
                        continue;
                    }

                    if (!File.Exists(file.Key))
                    {
                        Logger.LogInformation($"{filename} not found" + Environment.NewLine);
                        continue;
                    }

                    Logger.LogInformation($"{Environment.NewLine}Sending file: {file.Key}" + Environment.NewLine);
                    await WriteFile(
                            file.Key,
                            filename,
                            DefaultTimeout,
                            cancellationToken);
                }

                if (reUploadSkippedFiles)
                {
                    reUploadCounter++;
                    if (reUploadCounter < 3)
                    {
                        await DeployApp(applicationFilePath,
                            osVersion,
                            includePdbs,
                            verbose,
                            noLink,
                            cancellationToken);
                    }
                    else
                    {
                        // Clean up the reload variables.
                        reUploadSkippedFiles = false;
                        reUploadCounter = 0;

                        // Let's bail out of here
                        throw new Exception("Tried to send files to Meadow at least 3 times and failed. Something it seriously wrong! Check you are running the latest OS etc.");
                    }
                }
                else
                {
                    Logger.LogInformation($"{Environment.NewLine}{fileName} deploy complete!{Environment.NewLine}");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"An error occurred during the App deployment process.");
                Logger.LogError($"Error:{Environment.NewLine}{ex.Message} {Environment.NewLine}Stack Trace :{Environment.NewLine}{ex.StackTrace}");
                throw ex;
            }
        }

        public async Task<bool> IsFileOnDevice(string filename, CancellationToken cancellationToken)
        {
            if (FilesOnDevice.Any() == false)
            {
                await GetFilesAndCrcs(DefaultTimeout, 0, cancellationToken);
            }
            return FilesOnDevice.Any(f => f.FileName == filename);
        }
    }
}
