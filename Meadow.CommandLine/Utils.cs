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
        private readonly MeadowDeviceManager _meadowDeviceManager;
        private readonly ILogger<Utils> _logger;
        private readonly ILoggerFactory _loggerFactory;

        public Utils(ILoggerFactory loggerFactory, MeadowDeviceManager meadowDeviceManager)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<Utils>();
            _meadowDeviceManager = meadowDeviceManager;
        }

        /// <summary>
        /// Send a command to the Meadow and wait for the Meadow to be ready
        /// </summary>
        /// <param name="device">The <see cref="MeadowDevice"/> to use</param>
        /// <param name="command">The command to execute against the meadow</param>
        /// <param name="timeout">How long to wait for the meadow to become ready</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> to cancel the operation</param>
        /// <returns>A <see cref="bool"/> indicating if the Meadow is ready</returns>
        public async Task<bool> SendCommandAndWaitForReady(MeadowDevice device,
                                                           Func<Task> command,
                                                           int timeout = 60_000,
                                                           CancellationToken cancellationToken =
                                                               default)
        {
            _logger.LogTrace("Invoking command.");
            await command()
                .ConfigureAwait(false);

            _logger.LogTrace("Command invoked, waiting for Meadow to be ready.");

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
        public async Task<bool> WaitForReady(MeadowDevice device,
                                             int timeout = 60_000,
                                             CancellationToken cancellationToken = default)
        {
            if (device == null)
                throw new ArgumentNullException(nameof(device));

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
                    _logger.LogTrace(meadowCommandException.ToString());
                }
                catch (Exception ex)
                {
                    _logger.LogTrace($"An exception occurred. Retrying. Exception: {ex}");
                }

                await Task.Delay(1000, cancellationToken)
                          .ConfigureAwait(false);
            }

            throw new Exception($"Device not ready after {timeout}ms");
        }

        // Once flashed the meadow may take a minute or two to show up again as a serial port, and may show up under a different serial port than we think
        public async Task<MeadowDevice> FindMeadowBySerialNumber(
            string serialNumber,
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
                        var device = await _meadowDeviceManager.GetMeadowForSerialPort(
                                         port,
                                         true,
                                         cancellationToken);

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
                        _logger.LogTrace(
                            meadowDeviceException,
                            "This error can be safely ignored.");
                    }
                }

                await Task.Delay(1000, cancellationToken)
                          .ConfigureAwait(false);

                attempts++;
            }

            return null;
        }

        public async Task DisableMono(MeadowDevice device,
                                      CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Disabling Mono");

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

            _logger.LogInformation("Disabled Mono Successfully, app.exe will not run.");
        }

        public async Task EnableMono(MeadowDevice device,
                                     CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Enabling Mono");

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

            _logger.LogInformation("Enabled Mono Successfully, app.exe will run.");
        }

        public async Task ResetMeadow(MeadowDevice device,
                                      CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Resetting meadow");

            await SendCommandAndWaitForReady(
                    device,
                    () => device.ResetMeadow(cancellationToken),
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation("Successfully reset meadow.");
        }

        public async Task UpdateMonoRt(MeadowDevice device,
                                       string sourceFilename,
                                       CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Waiting for Meadow to be ready.");
            await WaitForReady(device, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await DisableMono(device, cancellationToken)
                .ConfigureAwait(false);

            Trace.Assert(
                await device.GetMonoRunState(cancellationToken)
                            .ConfigureAwait(false),
                "Meadow was expected to have Mono Disabled");

            _logger.LogInformation("Updating Mono Runtime");
            if (string.IsNullOrWhiteSpace(sourceFilename))
            {
                // check local override
                sourceFilename = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    DownloadManager.RuntimeFilename);

                if (File.Exists(sourceFilename))
                {
                    _logger.LogInformation(
                        $"Using current directory '{DownloadManager.RuntimeFilename}'");
                }
                else
                {
                    sourceFilename = Path.Combine(
                        DownloadManager.FirmwareDownloadsFilePath,
                        DownloadManager.RuntimeFilename);

                    if (File.Exists(sourceFilename))
                    {
                        _logger.LogInformation("FileName not specified, using latest download.");
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Unable to locate a runtime file. Either provide a path or download one.");

                        return; // KeepConsoleOpen?
                    }
                }
            }

            if (!File.Exists(sourceFilename))
            {
                _logger.LogInformation($"File '{sourceFilename}' not found");
                return; // KeepConsoleOpen?
            }

            await device.UpdateMonoRuntime(sourceFilename, cancellationToken: cancellationToken);

            await ResetMeadow(device, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task FlashEsp(MeadowDevice device,
                                   CancellationToken cancellationToken = default)
        {
            await WaitForReady(device, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await DisableMono(device, cancellationToken)
                .ConfigureAwait(false);

            Trace.Assert(
                await device.GetMonoRunState(cancellationToken)
                            .ConfigureAwait(false),
                "Meadow was expected to have Mono Disabled");

            _logger.LogInformation("Flashing ESP");

            _logger.LogInformation($"Transferring {DownloadManager.NetworkMeadowCommsFilename}");

            await device.WriteFileToEspFlash(
                            Path.Combine(
                                DownloadManager.FirmwareDownloadsFilePath,
                                DownloadManager.NetworkMeadowCommsFilename),
                            mcuDestAddr: "0x10000",
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);

            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);

            _logger.LogInformation($"Transferring {DownloadManager.NetworkBootloaderFilename}");

            await device.WriteFileToEspFlash(
                            Path.Combine(
                                DownloadManager.FirmwareDownloadsFilePath,
                                DownloadManager.NetworkBootloaderFilename),
                            mcuDestAddr: "0x1000",
                            cancellationToken: cancellationToken)
                        .ConfigureAwait(false);

            await Task.Delay(1000, cancellationToken)
                      .ConfigureAwait(false);

            _logger.LogInformation($"Transferring {DownloadManager.NetworkPartitionTableFilename}");

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