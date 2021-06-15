using System;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Devices;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement
{
    [Command("flash os", Description = "Update the OS on the Meadow Board")]
    public class FlashOsCommand : MeadowSerialCommand
    {
        public FlashOsCommand(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
        }

        [CommandOption("osFile", 'o', Description = "Path to the Meadow OS binary")]
        public string OsFile { get; init; }

        [CommandOption("runtimeFile", 'r', Description = "Path to the Meadow Runtime binary")]
        public string RuntimeFile { get; init; }

        [CommandOption("skipDfu",'d', Description = "Skip DFU flash.")]
        public bool SkipDfu { get; init; }

        [CommandOption("skipEsp", 'e', Description = "Skip ESP flash.")]
        public bool SkipEsp { get; init; }

        [CommandOption("skipRuntime", 'k', Description = "Skip updating the runtime.")]
        public bool SkipRuntime { get; init; }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            Meadow?.Dispose();

            string serialNumber;
            if (!SkipDfu)
                serialNumber = await MeadowDeviceHelper.DfuFlashAsync(SerialPortName, OsFile, Logger, cancellationToken).ConfigureAwait(false);
            else
            {
                Logger.LogInformation("Skipping DFU flash step.");
                using var device = await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, false).ConfigureAwait(false);
                if (device == null)
                {
                    Logger.LogWarning("Cannot find Meadow on {port}", SerialPortName);
                    return;
                }

                var deviceInfo = await device.GetDeviceInfoAsync(TimeSpan.FromSeconds(60), cancellationToken)
                                             .ConfigureAwait(false);

                serialNumber = deviceInfo!.SerialNumber;
            }

            var meadow = await MeadowDeviceManager.FindMeadowBySerialNumber(
                serialNumber,
                Logger,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            Meadow = new MeadowDeviceHelper(meadow, Logger);
            await Meadow.FlashOsAsync(RuntimeFile, SkipRuntime, SkipEsp, cancellationToken);
        }
    }
}