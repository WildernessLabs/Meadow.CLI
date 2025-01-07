using Meadow.Hcom;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Meadow.CLI.Core.Internals.Dfu;

public static class DfuUtils
{
    private static readonly int _osAddress = 0x08000000;
    public const string DEFAULT_DFU_VERSION = "0.11";
    private const string DFU_UTIL = "dfu-util";

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
            logger.LogError($"Unable to find file '{fileName}'. Please specify valid --File or download the latest with: meadow firmware download");
            return false;
        }

        logger.LogInformation($"Flashing OS with {fileName}");

        var dfuUtilVersion = new Version(await GetDfuUtilVersion(logger));
        logger.LogDebug("Detected OS: {os}", RuntimeInformation.OSDescription);

        var expectedDfuUtilVersion = new Version(DEFAULT_DFU_VERSION);
        if (dfuUtilVersion == null)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                logger.LogError($"{DFU_UTIL} not found - to install, run: `meadow dfu install` (may require administrator mode)");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                logger.LogError($"{DFU_UTIL} not found - to install run: `brew install {DFU_UTIL}`");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                logger.LogError($"{DFU_UTIL} not found - install using package manager, for example: `apt install {DFU_UTIL}` or the equivalent for your Linux distribution");
            }
            return false;
        }
        else if (dfuUtilVersion.CompareTo(expectedDfuUtilVersion) < 0)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                logger.LogError($"{DFU_UTIL} update required. Expected: {expectedDfuUtilVersion}, Found: {dfuUtilVersion} - to update, run in administrator mode: `meadow install {DFU_UTIL}`");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                logger.LogError($"{DFU_UTIL} update required. Expected: {expectedDfuUtilVersion}, Found: {dfuUtilVersion} - to update, run: `brew upgrade {DFU_UTIL}`");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                logger.LogError($"{DFU_UTIL} update required. Expected: {expectedDfuUtilVersion}, Found: {dfuUtilVersion} - to update , run: `apt upgrade {DFU_UTIL}` or the equivalent for your Linux distribution");
            }
            else
            {
                return false;
            }
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
            logger.LogError($"There was a problem executing {DFU_UTIL}: {ex.Message}");
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
            throw new Exception($"{DFU_UTIL} failed. Error: {ex.Message}");
        }
    }

    private static async Task<string> GetDfuUtilVersion(ILogger? logger)
    {
        try
        {
            var version = string.Empty;

            var result = await RunProcessCommand(DFU_UTIL, "--version",
                outputLogLine =>
                {
                    if (!string.IsNullOrWhiteSpace(outputLogLine)
                    && outputLogLine.StartsWith(DFU_UTIL))
                    {
                        var split = outputLogLine.Split(new char[] { ' ' });
                        if (split.Length == 2)
                        {
                            version = split[1];
                        }
                    }
                },
                errorLogLine =>
                {
                    if (!string.IsNullOrWhiteSpace(errorLogLine))
                    {
                        logger?.LogError(errorLogLine);
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

    public static async Task InstallDfuUtil(
        string tempFolder,
        string dfuUtilVersion = "0.11",
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, true);
            }

            using var client = new HttpClient();

            Directory.CreateDirectory(tempFolder);

            var downloadUrl = $"https://s3-us-west-2.amazonaws.com/downloads.wildernesslabs.co/public/{DFU_UTIL}-{dfuUtilVersion}-binaries.zip";

            var downloadFileName = downloadUrl.Substring(downloadUrl.LastIndexOf("/", StringComparison.Ordinal) + 1);

            var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.IsSuccessStatusCode == false)
            {
                throw new Exception($"Failed to download {DFU_UTIL}");
            }

            using (var stream = await response.Content.ReadAsStreamAsync())
            using (var downloadFileStream = new DownloadFileStream(stream))
            using (var fs = File.OpenWrite(Path.Combine(tempFolder, downloadFileName)))
            {
                await downloadFileStream.CopyToAsync(fs);
            }

            ZipFile.ExtractToDirectory(
                Path.Combine(tempFolder, downloadFileName),
                tempFolder);

            var is64Bit = Environment.Is64BitOperatingSystem;

            var dfuUtilExe = new FileInfo(
                Path.Combine(tempFolder, is64Bit ? "win64" : "win32", $"{DFU_UTIL}.exe"));

            var libUsbDll = new FileInfo(
                Path.Combine(
                    tempFolder,
                    is64Bit ? "win64" : "win32",
                    "libusb-1.0.dll"));

            var targetDir = is64Bit
                                ? Environment.GetFolderPath(Environment.SpecialFolder.System)
                                : Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);

            File.Copy(dfuUtilExe.FullName, Path.Combine(targetDir, dfuUtilExe.Name), true);
            File.Copy(libUsbDll.FullName, Path.Combine(targetDir, libUsbDll.Name), true);
        }
        finally
        {
            if (Directory.Exists(tempFolder))
            {
                Directory.Delete(tempFolder, true);
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