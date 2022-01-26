﻿using System;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Devices;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement
{
    [Command("flash os", Description = "Update the OS on the Meadow Board")]
    public class FlashOsCommand : MeadowSerialCommand
    {
        private MeadowDeviceInfo deviceInfo;
        private const string MINIMUM_OS_VERSION = "0.6.1.0";

        public FlashOsCommand(DownloadManager downloadManager, ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(downloadManager, loggerFactory, meadowDeviceManager)
        {
        }

        [CommandOption("osFile", 'o', Description = "Path to the Meadow OS binary")]
        public string OsFile { get; init; }

        [CommandOption("runtimeFile", 'r', Description = "Path to the Meadow Runtime binary")]
        public string RuntimeFile { get; init; }

        [CommandOption("skipDfu",'d', Description = "Skip DFU flash")]
        public bool SkipDfu { get; init; }

        [CommandOption("skipEsp", 'e', Description = "Skip ESP flash")]
        public bool SkipEsp { get; init; }

        [CommandOption("skipRuntime", 'k', Description = "Skip updating the runtime")]
        public bool SkipRuntime { get; init; }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            Meadow?.Dispose();

            string serialNumber = string.Empty;
            if (!SkipDfu)
            {
                serialNumber = await MeadowDeviceHelper.DfuFlashAsync(SerialPortName, OsFile, Logger, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                Logger.LogInformation("Skipping DFU flash step.");
                using var device = await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, false, Logger).ConfigureAwait(false);
                if (device == null)
                {
                    Logger.LogWarning("Cannot find Meadow on {port}", SerialPortName);
                    return;
                }

                deviceInfo = await device.GetDeviceInfoAsync(TimeSpan.FromSeconds(60), cancellationToken)
                    .ConfigureAwait(false);
                serialNumber = deviceInfo!.SerialNumber;
            }

            //try to find Meadow on the existing serial port first
            IMeadowDevice meadow = null;

            await Task.Delay(2000).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(SerialPortName) == false)
            {
                meadow = await MeadowDeviceManager.GetMeadowForSerialPort(
                    SerialPortName,
                    true,
                    Logger).ConfigureAwait(false);
            }

            if (meadow == null)
            {
                meadow = await MeadowDeviceManager.FindMeadowBySerialNumber(
                serialNumber,
                Logger,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            // If we don't have it yet, go and get it now, otherwise reuse deviceInfo we've got.
            if (deviceInfo == null)
                deviceInfo = await meadow.GetDeviceInfoAsync (TimeSpan.FromSeconds (60), cancellationToken)
                    .ConfigureAwait (false);

            // Get Current Device Version
            var currentOsVersion = new Version(deviceInfo?.MeadowOsVersion.Split (' ')[0]);

            // If less that B6.1 flash
            if (currentOsVersion.CompareTo (new Version(MINIMUM_OS_VERSION)) < 0) {
                // Do the funky chicken
                Logger.LogInformation ($"Your OS version is older than {MINIMUM_OS_VERSION}. A bulk flash erase is required." );
                await meadow.EraseFlashAsync (cancellationToken)
                    .ConfigureAwait (false);
            }

            await Task.Delay (2000).ConfigureAwait (false);

            Meadow = new MeadowDeviceHelper(meadow, Logger);

            await Meadow.FlashOsAsync(RuntimeFile, SkipRuntime, SkipEsp, cancellationToken);

            Meadow?.Dispose();
        }
    }
}