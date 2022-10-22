using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Internals.Dfu;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Meadow.CLI.Core
{
    public static class FirmwareManager
    {
        public static async Task<string?> GetRemoteFirmwareInfo(string versionNumber, ILogger logger)
        {
            var manager = new DownloadManager(logger);

            return await manager.DownloadMeadowOSVersionFile(versionNumber);
        }

        public static async Task GetRemoteFirmware(string versionNumber, ILogger logger)
        {
            var manager = new DownloadManager(logger);

            await manager.DownloadOsBinaries(versionNumber, true);
        }

        public static string GetLatestFirmwareVersion()
        {
            var di = new DirectoryInfo(DownloadManager.FirmwareDownloadsFilePathRoot);
            var latest = string.Empty;
            var latestFile = di.GetFiles("latest.txt").FirstOrDefault();
            if (latestFile != null)
            {
                latest = File.ReadAllText(latestFile.FullName).Trim();
            }
            return latest;
        }

        public static FirmwareInfo[] GetAllLocalFirmwareBuilds()
        {
            var list = new List<FirmwareInfo>();

            var di = new DirectoryInfo(DownloadManager.FirmwareDownloadsFilePathRoot);

            var latest = GetLatestFirmwareVersion();

            var options = new JsonSerializerOptions();
            options.Converters.Add(new BuildDateConverter());

            FirmwareInfo? ParseInfo(string version, string json)
            {
                var fi = JsonSerializer.Deserialize<FirmwareInfo>(json, options);
                if (fi == null) return null;
                fi.Version = version;
                fi.IsLatest = version == latest;
                return fi;
            }

            foreach (var dir in di.EnumerateDirectories())
            {
                var info = dir.GetFiles("build-info.json").FirstOrDefault();
                if (info == null) continue;
                var json = File.ReadAllText(info.FullName);
                try
                {
                    var fi = ParseInfo(dir.Name, json);
                    if (fi != null)
                    {
                        list.Add(fi);
                    }
                }
                catch (JsonException ex)
                {
                    // work around for Issue #229 (bad json)
                    var index = json.IndexOf(']');
                    if (index != -1 && json[index + 1] == ',')
                    {
                        var fix = $"{json.Substring(0, index + 1)}{json.Substring(index + 2)}";
                        try
                        {
                            var fi = ParseInfo(dir.Name, fix);
                            if (fi != null)
                            {
                                list.Add(fi);
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    continue;
                }
            }
            return list.ToArray();
        }

        public static FirmwareUpdater GetFirmwareUpdater(MeadowConnectionManager connectionManager)
        {
            return new FirmwareUpdater(connectionManager);
        }



        public static async Task PushApplicationToDevice(IMeadowConnection connection, DirectoryInfo appFolder, ILogger? logger = null)
        {
            try
            {
                if (connection == null) throw new ArgumentNullException("connection");
                if (connection.Device == null) throw new ArgumentNullException("connection.Device");
                if (!connection.IsConnected)
                {
                    if (!await connection.WaitForConnection(TimeSpan.FromSeconds(5)))
                    {
                        throw new Exception("No device connected");
                    }
                }

                connection.AutoReconnect = false;

                if (connection.Device.DeviceInfo == null)
                {
                    await connection.Device.GetDeviceInfo(TimeSpan.FromSeconds(5));
                }

                var osVersion = connection.Device.DeviceInfo.MeadowOsVersion;

                await connection.Device.MonoDisable();
                // the device will disconnect and reconnect here

                // can't check "is connected" immediately, as it take a few hundred ms to disable mono and restart
                await Task.Delay(1000);

                // wait for reconnect
                await connection.WaitForConnection(TimeSpan.FromSeconds(15));

                await connection.Device.DeployApp(Path.Combine(appFolder.FullName, "App.dll"), osVersion);

                await connection.Device.MonoEnable();
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error flashing OS to Meadow");
            }
            finally
            {
                connection.AutoReconnect = true;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="version">Either a specific version or null to push the latest</param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public static async Task PushFirmwareToDevice(MeadowConnectionManager connectionManager, IMeadowConnection? connection, string? version = null, ILogger? logger = null)
        {
            try
            {
                var dfuMode = connection == null;

                if (dfuMode)
                {
                    logger.LogInformation("Trying DFU Mode Firmware Update...");
                    DfuUtils.GetDevice();
                }
                else
                {
                    if (connection.Device == null) throw new ArgumentNullException("connection.Device");
                    if (!connection.IsConnected)
                    {
                        if (!await connection.WaitForConnection(TimeSpan.FromSeconds(5)))
                        {
                            throw new Exception("No device connected");
                        }
                    }
                    connection.AutoReconnect = false;

                    await connection.Device.EnterDfuMode();
                    // device resets here into DFU mode
                    // TODO: we need a "wait for DFU device is available"
                    // DfuUtils.CheckForValidDevice();
                    await Task.Delay(10000);
                }

                Debug.WriteLine("Starting DFU...");

                var success = await DfuUtils.DfuFlash(
                    string.Empty,
                    version,
                    null,
                    logger);

                Debug.WriteLine("DFU complete...");

                // device will reset here - need to reconnect
                await Task.Delay(10000);

                Debug.WriteLine("Looking for connection...");

                // if we started in DFU mode, we need to search for a serial connection - we have to assume that the first one we find is the Meadow
                while (connection == null)
                {
                    await Task.Delay(1000);
                    connection = connectionManager.FirstOrDefault();
                }

                Debug.WriteLine("Connecting...");

                connection.Connect();

                await Task.Delay(1000);

                Debug.WriteLine("Disabling Mono...");
                await connection.Device.MonoDisable();

                await Task.Delay(2000);
                Debug.WriteLine("Disconnecting...");
                connection.Disconnect();
                await Task.Delay(5000);

                Debug.WriteLine("Connecting...");
                connection.Connect();
                while (!connection.IsConnected)
                {
                    // wait for re-connection?
                    await Task.Delay(1000);
                }

                Debug.WriteLine("Updating runtime...");
                await connection.Device.UpdateMonoRuntime(null, version);

                await Task.Delay(2000);
                connection.Disconnect();
                await Task.Delay(5000);

                connection.Connect();
                while (!connection.IsConnected)
                {
                    // wait for re-connection?
                    await Task.Delay(1000);
                }

                await connection.Device.MonoDisable();

                await Task.Delay(2000);

                while (!connection.IsConnected)
                {
                    // wait for re-connection?
                    await Task.Delay(1000);
                }

                await connection.Device.FlashEsp(DownloadManager.FirmwareDownloadsFilePath, version);

                // Reset the meadow again to ensure flash worked.
                await connection.Device.ResetMeadow();

                await Task.Delay(2000);
                connection.Disconnect();
                await Task.Delay(5000);
            }
            catch (DeviceNotFoundException d)
            {
                throw d;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error flashing OS to Meadow");
            }
            finally
            {
                if (connection != null)
                {
                    connection.AutoReconnect = true;
                }
            }
        }
    }
}
