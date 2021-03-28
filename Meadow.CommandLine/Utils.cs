using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using CliFx.Infrastructure;
using Meadow.CLI;
using Meadow.CLI.Core;
using Meadow.CLI.Core.NewDeviceManagement;
using Microsoft.Extensions.Logging;

namespace Meadow.CommandLine
{
    public class Utils
    {
        /// <summary>
        /// Send a command to the Meadow and wait for the Meadow to be ready
        /// </summary>
        /// <param name="device">The <see cref="MeadowDevice"/> to use</param>
        /// <param name="command">The command to execute against the meadow</param>
        /// <param name="timeout">How long to wait for the meadow to become ready</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation</param>
        /// <returns>A <see cref="bool"/> indicating if the Meadow is ready</returns>
        public static async Task<bool> SendCommandAndWaitForReady(
            MeadowDevice device,
            Func<Task> command,
            int timeout = 60_000,
            CancellationToken cancellationToken = default)
        {
            Trace.WriteLine("Invoking command.");
            await command()
                .ConfigureAwait(false);

            Trace.WriteLine("Command invoked, waiting for Meadow to be ready.");
            
            return await WaitForReady(device, timeout, cancellationToken)
                       .ConfigureAwait(false);
        }

        /// <summary>
        /// Wait for the Meadow to respond to GetDeviceInfo
        /// </summary>
        /// <param name="device">The <see cref="MeadowDevice"/> to use</param>
        /// <param name="timeout">How long to wait for the meadow to become ready</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation</param>
        /// <returns>A <see cref="bool"/> indicating if the Meadow is ready</returns>
        public static async Task<bool> WaitForReady(MeadowDevice device,
                                                    int timeout = 60_000,
                                                    CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var then = now.AddMilliseconds(timeout);
            while (DateTime.UtcNow < then)
            {
                try
                {
                    var deviceInfo =
                        await device.GetDeviceInfo(cancellationToken: cancellationToken);

                    if (string.IsNullOrWhiteSpace(deviceInfo) == false)
                        return true;
                }
                catch (MeadowCommandException meadowCommandException)
                {
                    Trace.WriteLine(meadowCommandException.ToString());
                }
                catch (Exception ex)
                {
                    Trace.WriteLine($"An exception occurred. Retrying. Exception: {ex}");
                }

                await Task.Delay(1000, cancellationToken)
                          .ConfigureAwait(false);
            }

            throw new Exception($"Device not ready after {timeout}ms");
        }

        // Once flashed the meadow may take a minute or two to show up again as a serial port, and may show up under a different serial port than we think
        public static async Task<MeadowDevice> FindMeadowBySerialNumber(
            string serialNumber,
            ILoggerFactory loggerFactory,
            int maxAttempts = 10,
            CancellationToken cancellationToken = default)
        {
            var attempts = 0;
            while (attempts < maxAttempts)
            {
                var ports = SerialPort.GetPortNames();
                foreach (var port in ports)
                {
                    try
                    {
                        var device = await MeadowDeviceManager.GetMeadowForSerialPort(
                                         port,
                                         true,
                                         loggerFactory,
                                         cancellationToken:cancellationToken);

                        var deviceInfo =
                            await device.GetDeviceInfo(cancellationToken: cancellationToken);

                        if (deviceInfo.Contains(serialNumber))
                        {
                            return device;
                        }

                        device.Dispose();
                    }
                    catch (MeadowDeviceException meadowDeviceException)
                    {
                        // eat it for now
                        Trace.WriteLine(meadowDeviceException);
                    }
                }

                await Task.Delay(1000, cancellationToken)
                          .ConfigureAwait(false);

                attempts++;
            }

            return null;
        }

        public static async Task DisableMono(IConsole console,
                                             MeadowDevice device,
                                             CancellationToken cancellationToken = default)
        {
            var done = false;
            await console.Output.WriteAsync("Disabling Mono")
                         .ConfigureAwait(false);

            var task = Task.Factory.StartNew(
                async () =>
                {
                    while (!done)
                    {
                        //await console.Output.WriteAsync(".")
                        //             .ConfigureAwait(false);

                        await Task.Delay(1000, cancellationToken)
                                  .ConfigureAwait(false);
                    }
                },
                cancellationToken);

            do
            {
                // Send the Mono Disable Command
                await SendCommandAndWaitForReady(
                        device,
                        () => device.MonoDisable(cancellationToken),
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                // Reset the Meadow
                //await SendCommandAndWaitForReady(
                //        device,
                //        () => device.ResetMeadow(cancellationToken),
                //        cancellationToken: cancellationToken)
                //    .ConfigureAwait(false);
                // Double check the mono run state to ensure mono is actually disabled
            } while (await device.GetMonoRunState(cancellationToken)
                                 .ConfigureAwait(false));

            done = true;
            await task;
            await console.Output.WriteLineAsync(
                "Disabled Mono Successfully, app.exe will not run.");
        }

        public static async Task EnableMono(IConsole console,
                                            MeadowDevice device,
                                            CancellationToken cancellationToken = default)
        {
            var done = false;
            await console.Output.WriteAsync("Enabling Mono")
                         .ConfigureAwait(false);

            var task = Task.Factory.StartNew(
                async () =>
                {
                    while (!done)
                    {
                        //await console.Output.WriteAsync(".")
                        //             .ConfigureAwait(false);

                        await Task.Delay(1000, cancellationToken)
                                  .ConfigureAwait(false);
                    }
                },
                cancellationToken);

            do
            {
                // Send the Mono Disable Command
                await SendCommandAndWaitForReady(
                        device,
                        () => device.MonoEnable(cancellationToken),
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                // Reset the Meadow
                //await SendCommandAndWaitForReady(
                //        device,
                //        () => device.ResetMeadow(cancellationToken),
                //        cancellationToken: cancellationToken)
                //    .ConfigureAwait(false);
                // Double check the mono run state to ensure mono is actually disabled
            } while (await device.GetMonoRunState(cancellationToken)
                                 .ConfigureAwait(false)
                  == false);

            done = true;
            await task;

            await console.Output.WriteLineAsync("Enabled Mono Successfully, app.exe will run.");
        }

        public static async Task ResetMeadow(IConsole console,
                                             MeadowDevice device,
                                             CancellationToken cancellationToken = default)
        {
            var done = false;
            await console.Output.WriteAsync("Resetting meadow")
                         .ConfigureAwait(false);

            var task = Task.Factory.StartNew(
                async () =>
                {
                    while (!done)
                    {
                        await console.Output.WriteAsync(".")
                                     .ConfigureAwait(false);

                        await Task.Delay(1000, cancellationToken)
                                  .ConfigureAwait(false);
                    }
                },
                cancellationToken);

            await SendCommandAndWaitForReady(
                    device,
                    () => device.ResetMeadow(cancellationToken),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            done = true;
            await task;

            await console.Output.WriteLineAsync("Successfully reset meadow.");
        }

        public static async Task UpdateMonoRt(IConsole console,
                                              MeadowDevice device,
                                              string sourceFilename,
                                              CancellationToken cancellationToken = default)
        {
            await console.Output.WriteLineAsync("Waiting for Meadow to be ready.");
            await WaitForReady(device, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await DisableMono(console, device, cancellationToken)
                .ConfigureAwait(false);

            Trace.Assert(
                await device.GetMonoRunState(cancellationToken)
                            .ConfigureAwait(false),
                "Meadow was expected to have Mono Disabled");

            await console.Output.WriteLineAsync("Updating Mono Runtime");
            if (string.IsNullOrWhiteSpace(sourceFilename))
            {
                // check local override
                sourceFilename = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    DownloadManager.RuntimeFilename);

                if (File.Exists(sourceFilename))
                {
                    await console.Output.WriteLineAsync(
                        $"Using current directory '{DownloadManager.RuntimeFilename}'");
                }
                else
                {
                    sourceFilename = Path.Combine(
                        DownloadManager.FirmwareDownloadsFilePath,
                        DownloadManager.RuntimeFilename);

                    if (File.Exists(sourceFilename))
                    {
                        await console.Output.WriteLineAsync(
                            "FileName not specified, using latest download.");
                    }
                    else
                    {
                        await console.Output.WriteLineAsync(
                            "Unable to locate a runtime file. Either provide a path or download one.");

                        return; // KeepConsoleOpen?
                    }
                }
            }

            if (!File.Exists(sourceFilename))
            {
                await console.Output.WriteLineAsync($"File '{sourceFilename}' not found");
                return; // KeepConsoleOpen?
            }

            await device.UpdateMonoRuntime(sourceFilename, cancellationToken: cancellationToken);

            // Reset the meadow after updating the runtime
            await SendCommandAndWaitForReady(
                    device,
                    () => device.ResetMeadow(cancellationToken),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        public static async Task FlashEsp(IConsole console,
                                          MeadowDevice device,
                                          CancellationToken cancellationToken = default)
        {
            await WaitForReady(device, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await console.Output.WriteLineAsync("Disabling Mono");
            do
            {
                // Send the Mono Disable Command
                await SendCommandAndWaitForReady(
                        device,
                        () => device.MonoDisable(cancellationToken),
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                // Reset the Meadow
                await SendCommandAndWaitForReady(
                        device,
                        () => device.ResetMeadow(cancellationToken),
                        cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                // Double check the mono run state to ensure mono is actually disabled
            } while (await device.GetMonoRunState(cancellationToken)
                                 .ConfigureAwait(false));

            Trace.Assert(
                await device.GetMonoRunState(cancellationToken)
                            .ConfigureAwait(false),
                "Meadow was expected to have Mono Disabled");

            await console.Output.WriteLineAsync("Flashing ESP");

            await console.Output.WriteLineAsync(
                $"Transferring {DownloadManager.NetworkMeadowCommsFilename}");

            await device.WriteFileToEspFlash(
                            Path.Combine(
                                DownloadManager.FirmwareDownloadsFilePath,
                                DownloadManager.NetworkMeadowCommsFilename),
                            mcuDestAddr: "0x10000",
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);

            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);

            await console.Output.WriteLineAsync(
                $"Transferring {DownloadManager.NetworkBootloaderFilename}");

            await device.WriteFileToEspFlash(
                            Path.Combine(
                                DownloadManager.FirmwareDownloadsFilePath,
                                DownloadManager.NetworkBootloaderFilename),
                            mcuDestAddr: "0x1000",
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);

            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);

            await console.Output.WriteLineAsync(
                $"Transferring {DownloadManager.NetworkPartitionTableFilename}");

            await device.WriteFileToEspFlash(
                            Path.Combine(
                                DownloadManager.FirmwareDownloadsFilePath,
                                DownloadManager.NetworkPartitionTableFilename),
                            mcuDestAddr: "0x8000",
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);

            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);
        }
    }
}