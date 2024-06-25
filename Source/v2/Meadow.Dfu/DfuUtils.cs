using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Meadow.Hcom;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static System.Net.Mime.MediaTypeNames;

namespace Meadow.CLI.Core.Internals.Dfu;

public static class DfuUtils
{
    private static readonly int _osAddress = 0x08000000;

    private const string DFU_UTIL_UBUNTU_AMD64_URL = "http://ftp.de.debian.org/debian/pool/main/d/dfu-util/dfu-util_0.11-1_amd64.deb";
    private const string DFU_UTIL_UBUNTU_ARM64_URL = "http://ftp.de.debian.org/debian/pool/main/d/dfu-util/dfu-util_0.11-1_arm64.deb";
    private const string DFU_UTIL_WINDOWS_URL = $"https://s3-us-west-2.amazonaws.com/downloads.wildernesslabs.co/public/dfu-util-{DEFAULT_DFU_VERSION}-binaries.zip";
    private const string DFU_UTIL = "dfu-util";
    private const int STREAM_BUFFER_SIZE = 81920; // 80 KB buffer size

    public const string DEFAULT_DFU_VERSION = "0.11";

    public enum DfuFlashFormat
    {
        /// <summary>
        /// Percentage only
        /// </summary>
        Percent,
        /// <summary>
        /// Full console output, no formatting
        /// </summary>
        Full,
        /// <summary>
        /// Console.WriteLine for CLI - ToDo - remove
        /// </summary>
        ConsoleOut,
        /// <summary>
        /// No Console Output
        /// </summary>
        None,
    }

    private static void FormatDfuOutput(string logLine, ILogger? logger, DfuFlashFormat format = DfuFlashFormat.Percent)
    {
        if (format == DfuFlashFormat.Full)
        {
            logger?.LogInformation(logLine);
        }
        else if (format == DfuFlashFormat.Percent)
        {
            if (logLine.Contains("%"))
            {
                var progressBarEnd = logLine.IndexOf("]", StringComparison.Ordinal) + 1;
                var progress = logLine.Substring(progressBarEnd, logLine.IndexOf("%", StringComparison.Ordinal) - progressBarEnd + 1).TrimStart();

                if (progress != "100%")
                {
                    logger?.LogInformation(progress);
                }
            }
            else
            {
                logger?.LogInformation(logLine);
            }
        }
        else //Console out
        {
            if (!logLine.Contains("Device's firmware is corrupt") &&
                !logLine.Contains("null"))
            {
                Console.Write(logLine);
                Console.Write(logLine.Contains("%") ? "\r" : "\r\n");
            }
        }
    }

    public static async Task<bool> FlashFile(string fileName, string? dfuSerialNumber, ILogger? logger = null, DfuFlashFormat format = DfuFlashFormat.Percent)
    {
        logger ??= NullLogger.Instance;

        if (!File.Exists(fileName))
        {
            logger.LogError($"Unable to find file '{fileName}'. Please specify valid --File or download the latest with: meadow download os");
            return false;
        }

        logger.LogInformation($"Flashing OS with {fileName}");

        var dfuUtilVersion = new Version(await GetDfuUtilVersion());
        logger.LogDebug("Detected OS: {os}", RuntimeInformation.OSDescription);

        var expectedDfuUtilVersion = new Version(DEFAULT_DFU_VERSION);
        if (dfuUtilVersion == null)
        {
            logger.LogError("dfu-util not found - to install, run: `meadow dfu install`");
            return false;
        }
        else if (dfuUtilVersion.CompareTo(expectedDfuUtilVersion) < 0)
        {
            logger.LogError($"dfu-util update required. Expected: {expectedDfuUtilVersion}, Found: {dfuUtilVersion}  - to update, run: `meadow dfu install`");
            return false;
        }

        try
        {
            string args;

            if (string.IsNullOrWhiteSpace(dfuSerialNumber))
            {
                args = $"-a 0 -D \"{fileName}\" -s {_osAddress}:leave";
            }
            else
            {
                args = $"-a 0 -S {dfuSerialNumber} -D \"{fileName}\" -s {_osAddress}:leave";
            }

            await RunDfuUtil(args, logger, format);
        }
        catch (Exception ex)
        {
            logger.LogError($"There was a problem executing dfu-util: {ex.Message}");
            return false;
        }

        return true;
    }

    private static async Task RunDfuUtil(string args, ILogger? logger, DfuFlashFormat format = DfuFlashFormat.Percent)
    {
        try
        {
            var result = await RunProcessCommand(DFU_UTIL, args,
                outputLogLine =>
                {
                    // Ignore empty output
                    if (!string.IsNullOrWhiteSpace(outputLogLine)
                    && format != DfuFlashFormat.None)
                    {
                        FormatDfuOutput(outputLogLine, logger, format);
                    }
                },
                errorLogLine =>
                {
                    if (!string.IsNullOrWhiteSpace(errorLogLine))
                    {
                        logger?.LogError(errorLogLine);
                    }
                });
        }
        catch (Exception ex)
        {
            throw new Exception($"dfu-util failed. Error: {ex.Message}");
        }
    }

    private static async Task<string> GetDfuUtilVersion()
    {
        try
        {
            var version = string.Empty;

            var result = await RunProcessCommand(DFU_UTIL, "--version",
                output =>
                {
                    if (!string.IsNullOrWhiteSpace(output)
                    && output.StartsWith(DFU_UTIL))
                    {
                        var split = output.Split(new char[] { ' ' });
                        if (split.Length == 2)
                        {
                            version = split[1];
                        }
                    }
                });

            return string.IsNullOrWhiteSpace(version) ? string.Empty : version;
        }
        catch (Win32Exception ex)
        {
            switch (ex.NativeErrorCode)
            {
                case 0x0002: // ERROR_FILE_NOT_FOUND
                case 0x0003: // ERROR_PATH_NOT_FOUND
                case 0x000F: // ERROR_INVALID_DRIVE
                case 0x0014: // ERROR_BAD_UNIT
                case 0x001B: // ERROR_SECTOR_NOT_FOUND
                case 0x0033: // ERROR_REM_NOT_LIST
                case 0x013D: // ERROR_MR_MID_NOT_FOUND
                    return string.Empty;

                default:
                    throw;
            }
        }
    }

    public static async Task<bool> CheckIfDfuUtilIsInstalledOnWindows(
        string tempFolder,
        string dfuUtilVersion = DEFAULT_DFU_VERSION,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        // Check if dfu-util is installed.
        bool isDfuUtilInstalled = await IsCommandInstalled(DFU_UTIL);
        if (isDfuUtilInstalled)
        {
            var version = await GetDfuUtilVersion();
            if (!dfuUtilVersion.Equals(version))
            {
                return await InstallDfuUtilOnWindows(tempFolder, dfuUtilVersion, cancellationToken);
            }

            return true;
        }
        else
        {
            return await InstallDfuUtilOnWindows(tempFolder, dfuUtilVersion, cancellationToken);
        }
    }

    private static async Task<bool> InstallDfuUtilOnWindows(string tempFolder, string dfuUtilVersion, CancellationToken cancellationToken)
    {
        try
        {
            await DeleteDirectory(tempFolder);

            using var client = new HttpClient();

            Directory.CreateDirectory(tempFolder);

            var downloadedFileName = Path.GetFileName(new Uri(DFU_UTIL_WINDOWS_URL).LocalPath);
            var downloadedFilePath = Path.Combine(tempFolder, downloadedFileName);
            await DownloadFile(DFU_UTIL_WINDOWS_URL, downloadedFilePath, cancellationToken);

            await Task.Run(() =>
            {
                ZipFile.ExtractToDirectory(
                downloadedFilePath,
                tempFolder);
            });

            var is64Bit = Environment.Is64BitOperatingSystem;

            var dfuUtilExe = new FileInfo(
                Path.Combine(tempFolder, is64Bit ? "win64" : "win32", "dfu-util.exe"));

            var libUsbDll = new FileInfo(
                Path.Combine(
                    tempFolder,
                    is64Bit ? "win64" : "win32",
                    "libusb-1.0.dll"));

            var targetDir = is64Bit
                                ? Environment.GetFolderPath(Environment.SpecialFolder.System)
                                : Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);

            using (FileStream sourceStream = new FileStream(dfuUtilExe.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, STREAM_BUFFER_SIZE, useAsync: true))
            {
                await CopyFile(sourceStream, Path.Combine(targetDir, dfuUtilExe.Name));
            }

            using (FileStream sourceStream = new FileStream(libUsbDll.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, STREAM_BUFFER_SIZE, useAsync: true))
            {
                await CopyFile(sourceStream, Path.Combine(targetDir, libUsbDll.Name));
            }

            return true;
        }
        finally
        {
            await DeleteDirectory(tempFolder);
        }
    }

    public static async Task<bool> CheckIfDfuUtilIsInstalledOnMac(
        string dfuUtilVersion = DEFAULT_DFU_VERSION,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        // Check if brew is intalled.
        bool isBrewInstalled = await IsCommandInstalled("brew");
        if (!isBrewInstalled)
        {
            await InstallHomebrewOnMac();
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        // Check if dfu-util is installed
        bool isDfuUtilInstalled = await IsCommandInstalled(DFU_UTIL);
        if (isDfuUtilInstalled)
        {
            var version = await GetDfuUtilVersion();
            if (!dfuUtilVersion.Equals(version))
            {
                return await InstallDfuUtilOnMac();
            }

            return true;
        }
        else
        {
            return await InstallDfuUtilOnMac();
        }
    }

    public static async Task<bool> IsCommandInstalled(string command)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await IsCommandInstalledOnWindows(command);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return await IsCommandInstalledOnNix(command);
        }
        else
        {
            // Unsupported platform
            Console.Error.WriteLine("Unsupported platform.");
            return false;
        }
    }

    public static async Task<bool> IsCommandInstalledOnNix(string command)
    {
        try
        {
            var exitCode = await RunProcessCommand("/bin/bash", $"-c \"which {command}\"");
            return exitCode == 0;
        }
        catch (Exception ex)
        {
            // Handle exceptions
            Console.WriteLine($"An error occurred: {ex.Message}");
            return false;
        }
    }

    public static async Task InstallHomebrewOnMac()
    {
        var exitCode = await RunProcessCommand("/bin/bash", "-c '/bin/bash -c \"$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)\"'");
        if (exitCode != 0)
        {
            throw new Exception($"Unable to install Homebrew. Error:{exitCode}");
        }
        Console.WriteLine("Homebrew installated successfully.");
    }

    public static async Task<bool> InstallDfuUtilOnMac()
    {
        // Use brew to to install dfu-util
        var exitCode = await RunProcessCommand("/bin/bash", "-c 'brew install dfu-util'");
        if (exitCode != 0)
        {
            throw new Exception($"Unable to install dfu-util. Error:{exitCode}");
        }
        return true;
    }

    public static async Task<bool> IsCommandInstalledOnWindows(string command)
    {
        try
        {
            // -Verb RunAs elevates the command and asks for UAC
            var exitCode = await RunProcessCommand("powershell", $"-Command \"Start-Process -Verb RunAs -FilePath '{command}'\"");
            return exitCode == 0;
        }
        catch (Exception ex)
        {
            // Handle exceptions
            Console.Error.WriteLine($"An error occurred: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> CheckIfDfuUtilIsInstalledOnLinux(
        string dfuUtilVersion = DEFAULT_DFU_VERSION,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        // Check if dfu-util is installed.
        bool isDfuUtilInstalled = await IsCommandInstalled(DFU_UTIL);
        if (isDfuUtilInstalled)
        {
            var version = await GetDfuUtilVersion();
            if (!dfuUtilVersion.Equals(version))
            {
                return await InstallPackageOnLinux(DFU_UTIL);
            }

            return true;
        }
        else
        {
            return await InstallPackageOnLinux(DFU_UTIL);
        }
    }

    private static async Task<bool> InstallPackageOnLinux(string package)
    {
        string osReleaseFile = "/etc/os-release";

        if (File.Exists(osReleaseFile))
        {
            var lines = File.ReadAllLines(osReleaseFile);
            var distroName = string.Empty;
            var distroVersion = string.Empty;

            foreach (var line in lines)
            {
                if (line.StartsWith("NAME="))
                {
                    distroName = line.Substring(5).Trim('"').ToLower();
                }
                else if (line.StartsWith("VERSION="))
                {
                    distroVersion = line.Substring(8).Trim('"');
                }
            }

            switch (distroName)
            {
                case "ubuntu":
                    // If need be we can check distroVersion here too

                    // Install the default package for this distro
                    await InstallPackageOnUbuntu(package);

                    // We check the version again, because on some versions of Ubuntu the default dfu-util version is 0.9 :(
                    var installedDfuUtilVersion = new Version(await GetDfuUtilVersion());
                    var expectedDfuUtilVersion = new Version(DEFAULT_DFU_VERSION);
                    if (installedDfuUtilVersion.CompareTo(expectedDfuUtilVersion) < 0)
                    {
                        var dfuPackageUrl = RuntimeInformation.OSArchitecture switch
                        {
                            Architecture.Arm64 => DFU_UTIL_UBUNTU_ARM64_URL,
                            Architecture.X64 => DFU_UTIL_UBUNTU_AMD64_URL,
                            _ => throw new PlatformNotSupportedException("Unsupported architecture")
                        };

                        var downloadedFileName = Path.GetFileName(new Uri(dfuPackageUrl).LocalPath);
                        var downloadedFilePath = Path.Combine(Path.GetTempPath(), downloadedFileName);
                        await DownloadFile(dfuPackageUrl, downloadedFilePath);

                        await InstallDownloadedDebianPackage(downloadedFilePath);

                        // We've finished with it, let's delete it.
                        await DeleteFile(downloadedFilePath);

                        var recentlyInstalledDfuUtilVersion = new Version(await GetDfuUtilVersion());
                        if (recentlyInstalledDfuUtilVersion.CompareTo(expectedDfuUtilVersion) != 0)
                        {
                            throw new Exception($"Unable to install the version {expectedDfuUtilVersion} of {package}.");
                        }
                    }

                    return true;

                default:
                    Console.WriteLine($"To install {package} on Linux, use your distro's package manager to install the {package} package");
                    return false;
            }
        }
        else
        {
            Console.Error.WriteLine($"The {osReleaseFile} file does not exist. unable to proceed");
            return false;
        }
    }

    private static async Task InstallDownloadedDebianPackage(string fileName)
    {
        await RunProcessCommand("sudo", $"dpkg -i {fileName}");
    }

    public static async Task DeleteFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        await Task.Run(() =>
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        });
    }

    public static async Task DeleteDirectory(string directoryPath, bool recursive = true)
    {
        if (string.IsNullOrEmpty(directoryPath))
        {
            throw new ArgumentException("Directory path cannot be null or empty.", nameof(directoryPath));
        }

        await Task.Run(() =>
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: recursive);
            }
        });
    }

    private static async Task DownloadFile(string downloadUrl, string downloadedFileName, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient();
        var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to download {downloadedFileName} from {downloadUrl}");
        }

        using Stream contentStream = await response.Content.ReadAsStreamAsync();
        await CopyFile(contentStream, downloadedFileName);
    }

    public static async Task CopyFile(Stream sourceStream, string destinationFilePath)
    {
        using (FileStream destinationStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, STREAM_BUFFER_SIZE, useAsync: true))
        {
            await sourceStream.CopyToAsync(destinationStream);
        }
    }

    public static async Task<int> InstallPackageOnUbuntu(string package)
    {
        return await RunProcessCommand("sudo", $"apt-get --reinstall install {package}");
    }

    public static async Task<int> RunProcessCommand(string command, string args, Action<string>? handleOutput = null, Action<string>? handleError = null)
    {
        var processStartInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = new Process { StartInfo = processStartInfo })
        {
            process.Start();

            var outputCompletion = ReadLinesAsync(process.StandardOutput, handleOutput);
            var errorCompletion = ReadLinesAsync(process.StandardError, handleError);

            await Task.WhenAll(outputCompletion, errorCompletion, process.WaitForExitAsync());

            return process.ExitCode;
        }
    }

    private static async Task ReadLinesAsync(StreamReader reader, Action<string>? handleLine)
    {
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (!string.IsNullOrWhiteSpace(line)
                && handleLine != null)
            {
                handleLine(line);
            }
        }
    }
}

public static class ProcessExtensions
{
    public static Task<bool> WaitForExitAsync(this Process process)
    {
        var tcs = new TaskCompletionSource<bool>();

        process.EnableRaisingEvents = true;
        process.Exited += (sender, args) =>
        {
            tcs.SetResult(process.ExitCode == 0);
        };

        return tcs.Task;
    }
}