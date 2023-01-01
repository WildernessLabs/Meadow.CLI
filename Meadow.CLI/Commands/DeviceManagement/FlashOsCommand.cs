using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Devices;
using Meadow.CLI.Core.Internals.Dfu;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Meadow.CLI.Commands.DeviceManagement
{
    [Command("flash os", Description = "Update the OS on the Meadow Board")]
    public class FlashOsCommand : MeadowSerialCommand
    {
        private const string MINIMUM_OS_VERSION = "0.6.1.0";

        public FlashOsCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
        {
        }

        [CommandOption("osFile", 'o', Description = "Path to the Meadow OS binary")]
        public string OSFile { get; init; }

        [CommandOption("runtimeFile", 'r', Description = "Path to the Meadow Runtime binary")]
        public string RuntimeFile { get; init; }

        [CommandOption("skipDfu", 'd', Description = "Skip DFU flash")]
        public bool SkipDfu { get; init; }

        [CommandOption("skipEsp", 'e', Description = "Skip ESP flash")]
        public bool SkipEsp { get; init; }

        [CommandOption("skipRuntime", 'k', Description = "Skip updating the runtime")]
        public bool SkipRuntime { get; init; }

        [CommandOption("dontPrompt", 'p', Description = "Don't show bulk erase prompt")]
        public bool DontPrompt { get; init; }

        [CommandOption("osVersion", 'v', Description = "Flash a specific downloaded OS version - x.x.x.x")]
        public string OSVersion { get; init; }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            Meadow?.Dispose();

            string serialNumber = string.Empty;

            if (!SkipDfu)
            {
                try
                {
                    if (string.IsNullOrEmpty(OSFile) == false)
                    {
                        await DfuUtils.FlashFile(fileName: OSFile, logger: Logger, format: DfuUtils.DfuFlashFormat.ConsoleOut);
                    }
                    else if (string.IsNullOrEmpty(OSVersion) == false)
                    {
                        await DfuUtils.FlashVersion(version: OSVersion, logger: Logger, DfuUtils.DfuFlashFormat.ConsoleOut);
                    }
                    else
                    {
                        await DfuUtils.FlashLatest(logger: Logger, format: DfuUtils.DfuFlashFormat.ConsoleOut);
                    }
                    serialNumber = DfuUtils.LastSerialNumber;
                }
                catch
                {
                    Logger.LogInformation("Unable to flash Meadow OS");
                    return;
                }
            }
            else
            {
                Logger.LogInformation("Skipping step to flash Meadow OS");
            }

            //try to find Meadow on the existing serial port first
            IMeadowDevice meadow = null;

            if (string.IsNullOrWhiteSpace(SerialPortName) == false)
            {
                meadow = await MeadowDeviceManager.GetMeadowForSerialPort(
                    SerialPortName,
                    true,
                    Logger);
            }

            if (meadow == null)
            {
                meadow = await MeadowDeviceManager.FindMeadowBySerialNumber(
                serialNumber,
                Logger,
                cancellationToken: cancellationToken);
            }

            Meadow = new MeadowDeviceHelper(meadow, Logger);

            // Get Previous OS Version
            // We just flashed the OS so it will show the current version 
            // But the runtime hasn't been updated yet so should match the previous OS version
            Version previousOsVersion;
            var checkName = string.Empty;

            try
            {
                previousOsVersion = new Version(Meadow.DeviceInfo?.RuntimeVersion.Split(',')[0]);
                checkName = "runtime";
            }
            catch
            {
                previousOsVersion = new Version(MINIMUM_OS_VERSION);
                checkName = "OS";
            }

            // If less that B6.1 flash
            if (previousOsVersion.CompareTo(new Version(MINIMUM_OS_VERSION)) <= 0)
            {
                // Ask User 1st before wiping
                Logger.LogInformation($"Your {checkName} version is older than {MINIMUM_OS_VERSION} (or unreadable). A flash erase is highly recommended.");
                var yesOrNo = "y";

                if (!DontPrompt)
                {
                    Logger.LogInformation($"Proceed? (Y/N) Press Y to erase flash, N to continue install without erasing");
                    yesOrNo = await console.Input.ReadLineAsync();
                }

                if (yesOrNo.ToLower() == "y")
                {
                    await Meadow.MeadowDevice.EraseFlash(cancellationToken);

                    /* TODO EraseFlashAsync leaves the port in a dodgy state, so we need to kill it and find it again
                    Need a more elegant solution here. */
                    Meadow?.Dispose();
                    Meadow = null;

                    await Task.Delay(2000);

                    var device = await MeadowDeviceManager.FindMeadowBySerialNumber(
                            serialNumber,
                            Logger,
                            cancellationToken: cancellationToken);

                    if (device == null)
                    {
                        Logger.LogInformation($"Meadow device not found. Please plug in your meadow device and run this command again.");
                        return;
                    }

                    Meadow = new MeadowDeviceHelper(device, Logger);
                }
            }

            await Meadow.WriteRuntimeAndEspBins(RuntimeFile, OSVersion, SkipRuntime, SkipEsp, cancellationToken);

            Meadow?.Dispose();
        }
    }
}