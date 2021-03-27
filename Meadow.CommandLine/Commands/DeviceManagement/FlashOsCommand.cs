using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using CliFx.Attributes;
using CliFx.Infrastructure;
using LibUsbDotNet.Main;
using Meadow.CLI.Core;
using MeadowCLI;
using MeadowCLI.DeviceManagement;

namespace Meadow.CommandLine.Commands.DeviceManagement
{
    [Command("flash os", Description = "Update the OS on the Meadow Board")]
    public class FlashOsCommand : MeadowSerialCommand
    {
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
                    using var device =
                        await MeadowDeviceManager.GetMeadowForSerialPort(SerialPortName, true, cancellationToken);

                    await console.Output.WriteLineAsync("Entering DFU Mode");
                    await MeadowDeviceManager.ProcessCommand(
                        device,
                        MeadowFileManager.HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENTER_DFU_MODE,
                        null,
                        cancellationToken: cancellationToken);
                }
                catch (FileNotFoundException)
                {
                    Debug.WriteLine("Failed to find Serial Port.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }

                switch (dfuAttempts)
                {
                    case 5:
                        await console.Output.WriteAsync(
                            "Having trouble putting Meadow in DFU Mode, please press RST button on Meadow and press enter to try again");

                        await console.Input.ReadLineAsync();
                        break;
                    case 10:
                        await console.Output.WriteAsync(
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

            await console.Output.WriteLineAsync("Device in DFU Mode, flashing OS");
            DfuUtils.FlashOS(device: dfuDevice);
            await console.Output.WriteLineAsync("Device Flashed.");

            try
            {
                using var device = await Utils.FindMeadowBySerialNumber(
                                                  serialNumber,
                                                  cancellationToken: cancellationToken)
                                              .ConfigureAwait(false);

                await Utils.UpdateMonoRt(console, device, BinPath, cancellationToken);

                // Again, verify that Mono is disabled
                Trace.Assert(
                    await MeadowDeviceManager.MonoRunState(device, cancellationToken)
                                             .ConfigureAwait(false),
                    "Meadow was expected to have Mono Disabled");

                await console.Output.WriteLineAsync("Flashing ESP");
                await Utils.FlashEsp(console, device, cancellationToken)
                           .ConfigureAwait(false);

                // Reset the meadow again to ensure flash worked.
                await Utils.ResetMeadow(console, device, cancellationToken)
                           .ConfigureAwait(false);

                await console.Output.WriteLineAsync("Enabling Mono and Resetting.");
                while (await MeadowDeviceManager.MonoRunState(device, cancellationToken)
                                                .ConfigureAwait(false)
                    == false)
                {
                    await Utils.EnableMono(console, device, cancellationToken);
                }

                // TODO: Verify that the device info returns the expected version
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