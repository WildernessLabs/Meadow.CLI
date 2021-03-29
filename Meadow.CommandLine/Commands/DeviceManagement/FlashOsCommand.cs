using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using LibUsbDotNet.Main;
using Meadow.CLI.Core;
using MeadowCLI;
using Meadow.CLI.Core.NewDeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CommandLine.Commands.DeviceManagement
{
    [Command("flash os", Description = "Update the OS on the Meadow Board")]
    public class FlashOsCommand : MeadowSerialCommand
    {
        private readonly ILogger<FlashOsCommand> _logger;

        public FlashOsCommand(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
            : base(loggerFactory, meadowDeviceManager)
        {
            _logger = loggerFactory.CreateLogger<FlashOsCommand>();
        }

        [CommandOption("BinPath", 'b', Description = "Path to the Meadow OS binary")]
        public string BinPath { get; init; }

        public override async ValueTask ExecuteAsync(IConsole console)
        {
            var cancellationToken = console.RegisterCancellationHandler();

            var dfuAttempts = 0;
            UsbRegistry dfuDevice = null;
            while (true)
            {
                try
                {
                    try
                    {
                        dfuDevice = DfuUtils.GetDevice();
                        break;
                    }
                    catch (MultipleDfuDevicesException)
                    {
                        // This is bad, we can't just blindly flash with multiple devices, let the user know
                        throw;
                    }
                    catch (DeviceNotFoundException)
                    {
                        // eat it.
                    }

                    // No DFU device found, lets try to set the meadow to DFU mode.
                    using var device = await MeadowDeviceManager.GetMeadowForSerialPort(
                                           SerialPortName,
                                           true,
                                           cancellationToken);

                    _logger.LogInformation("Entering DFU Mode");
                    await device.EnterDfuMode(cancellationToken)
                                .ConfigureAwait(false);
                }
                catch (FileNotFoundException)
                {
                    _logger.LogError("Failed to find Serial Port.");
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(
                        "An exception occurred while switching device to DFU Mode. Exception: {0}",
                        ex);
                }

                switch (dfuAttempts)
                {
                    case 5:
                        _logger.LogInformation(
                            "Having trouble putting Meadow in DFU Mode, please press RST button on Meadow and press enter to try again");

                        await console.Input.ReadLineAsync();
                        break;
                    case 10:
                        _logger.LogInformation(
                            "Having trouble putting Meadow in DFU Mode, please hold BOOT button, press RST button and release BOOT button on Meadow and press enter to try again");

                        await console.Input.ReadLineAsync();
                        break;
                    case > 15:
                        throw new Exception(
                            "Unable to place device in DFU mode, please disconnect the Meadow, hold the BOOT button, reconnect the Meadow, release the BOOT button and try again.");
                }

                // Lets give the device a little time to settle in and get picked up
                await Task.Delay(1000, cancellationToken)
                          .ConfigureAwait(false);

                dfuAttempts++;
            }

            // Get the serial number so that later we can pick the right device if the system has multiple meadow plugged in
            var serialNumber = DfuUtils.GetDeviceSerial(dfuDevice);

            _logger.LogInformation("Device in DFU Mode, flashing OS");
            DfuUtils.FlashOS(device: dfuDevice);
            _logger.LogInformation("Device Flashed.");

            try
            {
                using var device = await MeadowDeviceManager.FindMeadowBySerialNumber(
                                                                serialNumber,
                                                                cancellationToken:
                                                                cancellationToken)
                                                            .ConfigureAwait(false);

                Trace.Assert(device != null, "device != null");

                await device.UpdateMonoRuntime(BinPath, cancellationToken: cancellationToken);

                // Again, verify that Mono is disabled
                Trace.Assert(
                    await device.GetMonoRunState(cancellationToken)
                                .ConfigureAwait(false),
                    "Meadow was expected to have Mono Disabled");

                _logger.LogInformation("Flashing ESP");
                await device.FlashEsp(cancellationToken)
                            .ConfigureAwait(false);

                // Reset the meadow again to ensure flash worked.
                await device.ResetMeadow(cancellationToken)
                           .ConfigureAwait(false);

                _logger.LogInformation("Enabling Mono and Resetting.");
                while (await device.GetMonoRunState(cancellationToken)
                                   .ConfigureAwait(false)
                    == false)
                {
                    await device.MonoEnable(cancellationToken);
                }

                // TODO: Verify that the device info returns the expected version
                var deviceInfoString = await device
                                             .GetDeviceInfo(cancellationToken: cancellationToken)
                                             .ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(deviceInfoString))
                {
                    throw new Exception("Unable to retrieve device info.");
                }

                var deviceInfo = new MeadowDeviceInfo(deviceInfoString);
                _logger.LogInformation(
                    $"Updated Meadow to OS: {deviceInfo.MeadowOSVersion} ESP: {deviceInfo.CoProcessorOs}");

                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                console.Output.WriteLine(ex);
                Environment.Exit(-1);
            }
        }
    }
}