using CliFx.Attributes;
using CliFx.Infrastructure;
using LibUsbDotNet.LibUsb;
using Meadow.CLI.Commands.Utility;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Devices;
using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Internals.Dfu;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
        public bool SkipOS { get; init; }

        [CommandOption("skipEsp", 'e', Description = "Skip ESP flash")]
        public bool SkipEsp { get; init; }

        [CommandOption("skipRuntime", 'k', Description = "Skip updating the runtime")]
        public bool SkipRuntime { get; init; }

        [CommandOption("dontPrompt", 'p', Description = "Don't show bulk erase prompt")]
        public string DontPrompt { get; init; }

        [CommandOption("osVersion", 'v', Description = "Flash a specific downloaded OS version - x.x.x.x")]
        public string OSVersion { get; init; }

        [CommandOption("serialNumber", 'n', Description = "Serial Number of device that will be flashed")]
        public string SerialNumber { get; init; }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            Meadow?.Dispose();

            Logger.LogInformation($"[STARTED] Flash of Meadow device with Serial Number : {SerialNumber}");
            if (!SkipOS)
            {
                // ToDo - restore two lines below when OS is fixed to succesfully set Dfu mode - broken as of RC2
                // await SetMeadowToDfuMode(SerialPortName, cancellationToken);
                // await Task.Delay(2000, cancellationToken);

                try
                {
                    await FlashOsInDfuMode();
                }
                catch (Exception ex)
                {
                    Logger.LogInformation($"Unable to flash Meadow OS on device : {SerialNumber} \n Error: {ex.Message}");
                    return;
                }

            }
            else
            {
                Logger.LogInformation("Skipping step to flash Meadow OS");
            }

            await Task.Delay(2000, cancellationToken);

            Meadow = await FindCurrentMeadowDevice(SerialPortName, SerialNumber, cancellationToken);

            var eraseFlash = await ValidateVersionAndPromptUserToEraseFlash(console, cancellationToken);

            if (eraseFlash)
            {
                // TODO We may want to move this into Meadow.EraseFlash() so the behaviour is centralised
                var spinnerCancellationTokenSource = new CancellationTokenSource();
                var consoleSpinner = new ConsoleSpinner();
                Task consoleSpinnerTask = consoleSpinner.Turn(250, spinnerCancellationTokenSource.Token);

                await Meadow.EraseFlash(cancellationToken);

                // Cancel the spinner as soon as EraseFlash finishes
                spinnerCancellationTokenSource.Cancel();

                // Let's start spinning
                await consoleSpinnerTask;

                Meadow?.Dispose();
                Meadow = null;

                await Task.Delay(2000, cancellationToken);

                Meadow = await FindCurrentMeadowDevice(SerialPortName, SerialNumber, cancellationToken);
            }

            await Meadow.WriteRuntimeAndEspBins(RuntimeFile, OSVersion, SkipRuntime, SkipEsp, cancellationToken);

            Meadow?.Dispose();

            Logger.LogInformation($"[COMPLETED] Flash of Meadow device with Serial Number : {SerialNumber}");
        }

        async Task<bool> ValidateVersionAndPromptUserToEraseFlash(IConsole console, CancellationToken cancellationToken)
        {
            // We just flashed the OS so it will show the current version 
            // But the runtime hasn't been updated yet so should match the previous OS version
            Version previousOsVersion;
            string checkName;

            try
            {
                previousOsVersion = new Version(Meadow.DeviceInfo?.RuntimeVersion.Split(' ')[0]);
                checkName = "runtime";
            }
            catch
            {
                previousOsVersion = new Version(MINIMUM_OS_VERSION);
                checkName = "OS";
            }

            Logger.LogInformation($"Previous OS {previousOsVersion}, Minimum OS {new Version(MINIMUM_OS_VERSION)}");
            // If less that B6.1 flash
            if (previousOsVersion.CompareTo(new Version(MINIMUM_OS_VERSION)) <= 0)
            {
                // Ask User 1st before wiping
                Logger.LogInformation($"Your {checkName} version is older than {MINIMUM_OS_VERSION} (or unreadable). A flash erase is highly recommended.");
                var yesOrNo = "y";

                if (string.IsNullOrEmpty(DontPrompt))
                {
                    Logger.LogInformation($"Proceed? (Y/N) Press Y to erase flash, N to continue install without erasing");
                    yesOrNo = await console.Input.ReadLineAsync();
                }
                else
                {
                    yesOrNo = DontPrompt;
                }

                if (yesOrNo.ToLower() == "y")
                {
                    return true;
                }
            }
            return false;
        }

        async Task<MeadowDeviceHelper> FindCurrentMeadowDevice(string serialPortName, string serialNumber, CancellationToken cancellationToken)
        {
            IMeadowDevice meadow = null;

            if (string.IsNullOrWhiteSpace(SerialPortName) == false)
            {
                meadow = await MeadowDeviceManager.GetMeadowForSerialPort(
                    serialPortName,
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

            return new MeadowDeviceHelper(meadow, Logger);
        }

        async Task SetMeadowToDfuMode(string serialPortName, CancellationToken cancellationToken)
        {
            var dfuAttempts = 0;

            IUsbDevice dfuDevice;
            while (true)
            {
                try
                {
                    try
                    {
                        dfuDevice = DfuUtils.GetDeviceInBootloaderMode();
                        break;
                    }
                    catch (MultipleDfuDevicesException)
                    {   // Can't determine device to flash
                        throw;
                    }
                    catch (DeviceNotFoundException)
                    {   // ignore and continue
                    }

                    // No DFU devices found - attempt to set the meadow to DFU mode
                    using var device = await MeadowDeviceManager.GetMeadowForSerialPort(serialPortName, false);

                    if (device != null)
                    {
                        Logger.LogInformation("Entering DFU Mode");
                        await device.EnterDfuMode(cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogDebug(
                        "An exception occurred while switching device to DFU Mode. Exception: {0}", ex);
                }

                switch (dfuAttempts)
                {
                    case 5:
                        Logger.LogInformation(
                            "Having trouble putting Meadow in DFU Mode, please press RST button on Meadow and press enter to try again");

                        Console.ReadKey();
                        break;
                    case 10:
                        Logger.LogInformation(
                            "Having trouble putting Meadow in DFU Mode, please hold BOOT button, press RST button and release BOOT button on Meadow and press enter to try again");

                        Console.ReadKey();
                        break;
                    case > 15:
                        throw new Exception(
                            "Unable to place device in DFU mode, please disconnect the Meadow, hold the BOOT button, reconnect the Meadow, release the BOOT button and try again.");
                }

                await Task.Delay(1000, cancellationToken);

                dfuAttempts++;
            }
        }

        async Task FlashOsInDfuMode()
        {
            var device = DfuUtils.GetDevicesInBootloadMode().Where(d => (string)d.DeviceProperties["SerialNumber"] == SerialNumber).FirstOrDefault();
            if (string.IsNullOrEmpty(OSFile) == false)
            {
                await DfuUtils.FlashFile(fileName: OSFile, device: device, logger: Logger, format: DfuUtils.DfuFlashFormat.ConsoleOut);
            }
            else if (string.IsNullOrEmpty(OSVersion) == false)
            {
                await DfuUtils.FlashVersion(version: OSVersion, device: device, logger: Logger, DfuUtils.DfuFlashFormat.ConsoleOut);
            }
            else
            {
                await DfuUtils.FlashLatest(device: device, logger: Logger, format: DfuUtils.DfuFlashFormat.ConsoleOut);
            }
        }
        
        string GetSerialNumber() => DfuUtils.LastSerialNumber;
    }
}