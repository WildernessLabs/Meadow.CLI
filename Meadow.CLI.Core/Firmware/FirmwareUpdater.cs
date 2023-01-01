﻿using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.Exceptions;
using Meadow.CLI.Core.Internals.Dfu;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Meadow.CLI.Core
{
    public class FirmwareUpdater
    {
        private MeadowConnectionManager _connectionManager;
        private ILogger? _logger;
        private Task? _updateTask;
        private IMeadowConnection _connection;
        private UpdateState _state;

        private string RequestedVersion { get; set; }

        public enum UpdateState
        {
            NotStarted,
            EnteringDFUMode,
            InDFUMode,
            UpdatingOS,
            DFUCompleted,
            DisablingMonoForRuntime,
            UpdatingRuntime,
            DisablingMonoForCoprocessor,
            UpdatingCoprocessor,
            AllWritesComplete,
            VerifySuccess,
            UpdateSuccess,
            Error
        }

        public UpdateState PreviousState { get; private set; }

        internal FirmwareUpdater(MeadowConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
            _logger = connectionManager.Logger;
        }

        public UpdateState CurrentState
        {
            get => _state;
            private set
            {
                if (value == _state) return;
                PreviousState = CurrentState;
                _state = value;
                _logger.LogDebug($"Firmware Updater: {PreviousState}->{CurrentState}");
            }
        }

        private async void StateMachine()
        {
            var tries = 0;

            MeadowDeviceInfo? info = null;

            while (true)
            {
                switch (CurrentState)
                {
                    case UpdateState.NotStarted:
                        try
                        {
                            // make sure we have a current device info
                            info = await _connection.Device.GetDeviceInfo(TimeSpan.FromSeconds(10));

                            if (info.MeadowOsVersion == RequestedVersion)
                            {
                                // no need to update, it's already there
                                CurrentState = UpdateState.DFUCompleted;
                                break;
                            }

                            // enter DFU mode
                            await _connection.Device.EnterDfuMode();
                            CurrentState = UpdateState.EnteringDFUMode;
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex.Message);
                            CurrentState = UpdateState.Error;
                            return;
                        }

                        break;
                    case UpdateState.EnteringDFUMode:
                        // look for DFU device
                        try
                        {
                            var dfu = DfuUtils.GetDevice();
                            CurrentState = UpdateState.InDFUMode;
                        }
                        catch (Exception ex)
                        {
                            ++tries;
                            if (tries > 5)
                            {
                                _logger.LogError($"Failed to enter DFU mode: {ex.Message}");
                                CurrentState = UpdateState.Error;

                                // exit state machine
                                return;
                            }
                            await Task.Delay(1000);
                        }
                        break;
                    case UpdateState.InDFUMode:
                        try
                        {
                            var success = await DfuUtils.FlashVersion(RequestedVersion, _logger);
                            if (success)
                            {
                                CurrentState = UpdateState.DFUCompleted;
                            }
                            else
                            {
                                CurrentState = UpdateState.Error;

                                // exit state machine
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex.Message);
                            CurrentState = UpdateState.Error;
                            return;
                        }
                        break;
                    case UpdateState.DFUCompleted:
                        // if we started in DFU mode, we'll have no connection.  We'll have to just assume the first one to appear is what we're after
                        try
                        {
                            if (_connection == null)
                            {
                                _connection = await GetFirstAvailableConnection();
                            }

                            // wait for device to reconnect
                            await _connection.WaitForConnection(TimeSpan.FromSeconds(30));
                            await Task.Delay(2000); // wait 2 seconds to allow full boot

                            if (info == null)
                            {
                                info = _connection.Device.DeviceInfo;
                            }

                            CurrentState = UpdateState.DisablingMonoForRuntime;
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex.Message);
                            CurrentState = UpdateState.Error;
                            return;
                        }
                        break;
                    case UpdateState.DisablingMonoForRuntime:
                        try
                        {
                            await _connection.Device.MonoDisable();
                        }
                        catch (DeviceDisconnectedException)
                        {
                            // this happens after calling MonoDisable because it tries to read a response from a non-existing serial port
                            // just ignore it, the next state waits for a reconnect anyway
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex.Message);
                            CurrentState = UpdateState.Error;
                            return;
                        }
                        CurrentState = UpdateState.UpdatingRuntime;
                        break;
                    case UpdateState.UpdatingRuntime:
                        if (info.RuntimeVersion == RequestedVersion)
                        {
                            // no need to update, it's already there
                        }
                        else
                        {
                            try
                            {
                                await _connection.WaitForConnection(TimeSpan.FromSeconds(30));
                                await Task.Delay(2000); // wait 2 seconds to allow full boot

                                if (info == null)
                                {
                                    info = _connection.Device.DeviceInfo;
                                }

                                await _connection.Device.UpdateMonoRuntime(null, RequestedVersion);
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex.Message);
                                CurrentState = UpdateState.Error;
                                return;
                            }
                        }
                        CurrentState = UpdateState.DisablingMonoForCoprocessor;
                        break;
                    case UpdateState.DisablingMonoForCoprocessor:
                        try
                        {
                            await _connection.Device.MonoDisable();

                            CurrentState = UpdateState.UpdatingCoprocessor;
                        }
                        catch (DeviceDisconnectedException)
                        {
                            // this happens after calling MonoDisable because it tries to read a response from a non-existing serial port
                            // just ignore it, the next state waits for a reconnect anyway
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex.Message);
                            CurrentState = UpdateState.Error;
                            return;
                        }
                        CurrentState = UpdateState.UpdatingCoprocessor;
                        break;
                    case UpdateState.UpdatingCoprocessor:
                        if (info.CoProcessorOsVersion == RequestedVersion)
                        {
                            // no need to update, it's already there
                        }
                        else
                        {
                            try
                            {
                                Debug.WriteLine(">> waiting for connection");
                                await _connection.WaitForConnection(TimeSpan.FromSeconds(30));
                                Debug.WriteLine(">> delay");
                                await Task.Delay(3000); // wait to allow full boot - no idea why this takes longer

                                if (info == null)
                                {
                                    Debug.WriteLine(">> query device info");
                                    info = _connection.Device.DeviceInfo;
                                }

                                Debug.WriteLine(">> flashing ESP");
                                await _connection.Device.FlashEsp(DownloadManager.FirmwareDownloadsFilePath, RequestedVersion);
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex.Message);
                                CurrentState = UpdateState.Error;
                                return;
                            }
                        }
                        CurrentState = UpdateState.AllWritesComplete;
                        break;
                    case UpdateState.AllWritesComplete:
                        try
                        {
                            await _connection.Device.ResetMeadow();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex.Message);
                            CurrentState = UpdateState.Error;
                            return;
                        }
                        break;
                        CurrentState = UpdateState.VerifySuccess;
                    case UpdateState.VerifySuccess:
                        try
                        {
                            await _connection.WaitForConnection(TimeSpan.FromSeconds(30));
                            await Task.Delay(2000); // wait 2 seconds to allow full boot
                            info = await _connection.Device.GetDeviceInfo(TimeSpan.FromSeconds(30));
                            if (info.MeadowOsVersion != RequestedVersion)
                            {
                                // this is a failure
                                _logger?.LogWarning($"OS version {info.MeadowOsVersion} does not match requested version {RequestedVersion}");
                            }
                            if (info.RuntimeVersion != RequestedVersion)
                            {
                                // this is a failure
                                _logger?.LogWarning($"Runtime version {info.RuntimeVersion} does not match requested version {RequestedVersion}");
                            }
                            if (info.CoProcessorOsVersion != RequestedVersion)
                            {
                                // not necessarily an error
                                _logger?.LogWarning($"Coprocessor version {info.CoProcessorOsVersion} does not match requested version {RequestedVersion}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex.Message);
                            CurrentState = UpdateState.Error;
                            return;
                        }
                        CurrentState = UpdateState.UpdateSuccess;
                        break;
                    case UpdateState.UpdateSuccess:
                        _connection.AutoReconnect = true;
                        _connection.MonitorState = true;
                        _logger?.LogInformation("Update complete");
                        return;
                    default:
                        break;
                }

                await Task.Delay(1000);
            }
        }

        private async Task<IMeadowConnection> GetFirstAvailableConnection()
        {
            IMeadowConnection connection = null;

            while (true)
            {
                connection = _connectionManager.FirstOrDefault();
                if (connection != null) return connection;
                await Task.Delay(1000);
            }
        }

        private async Task GetConnectionDevice(IMeadowConnection connection)
        {
            if (connection.Device != null) return;
            connection.Connect();
        }

        public Task Update(IMeadowConnection? connection, string? version = null)
        {
            string updateVersion;
            if (version == null)
            {
                // use "latest"
                updateVersion = FirmwareManager.GetLocalLatestFirmwareVersion();
            }
            else
            {
                // verify the version requested is valid
                var build = FirmwareManager.GetAllLocalFirmwareBuilds().FirstOrDefault(b => b.Version == version);
                if (build == null)
                {
                    throw new Exception($"Unknown build: '{version}'");
                }
                updateVersion = build.Version;
            }

            RequestedVersion = updateVersion;

            if (connection == null)
            {
                // assume DFU mode startup
                CurrentState = UpdateState.EnteringDFUMode;
            }
            else
            {
                _connection = connection;
                _connection.AutoReconnect = false;
                _connection.MonitorState = false;
                CurrentState = UpdateState.NotStarted;
            }

            return Task.Run(StateMachine);
        }
    }
}
