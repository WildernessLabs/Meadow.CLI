using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Meadow.Hcom;

public class FirmwareUpdater
{
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

    internal FirmwareUpdater(IMeadowConnection connection)
    {
        _connection = connection;
        //        _logger = connection.Logger;
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

        DeviceInfo? info = null;

        while (true)
        {
            switch (CurrentState)
            {
                case UpdateState.NotStarted:
                    try
                    {
                        // make sure we have a current device info
                        info = await _connection.Device.GetDeviceInfo();

                        if (info.OsVersion == RequestedVersion)
                        {
                            // no need to update, it's already there
                            CurrentState = UpdateState.DFUCompleted;
                            break;
                        }

                        // enter DFU mode
                        // await _connection.Device.EnterDfuMode();
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
                        //var dfu = DfuUtils.GetDeviceInBootloaderMode();
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
                        //var success = await DfuUtils.FlashVersion(RequestedVersion, _logger);
                        var success = false;
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
                        // wait for device to reconnect
                        await _connection.WaitForMeadowAttach();
                        await Task.Delay(2000); // wait 2 seconds to allow full boot

                        if (info == null)
                        {
                            info = await _connection.Device.GetDeviceInfo();
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
                        await _connection.Device.RuntimeDisable();
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
                            await _connection.WaitForMeadowAttach();
                            await Task.Delay(2000); // wait 2 seconds to allow full boot

                            if (info == null)
                            {
                                info = await _connection.Device.GetDeviceInfo();
                            }

                            await _connection.Device.FlashRuntime(RequestedVersion);
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
                        await _connection.Device.RuntimeDisable();

                        CurrentState = UpdateState.UpdatingCoprocessor;
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
                    if (info.CoprocessorOsVersion == RequestedVersion)
                    {
                        // no need to update, it's already there
                    }
                    else
                    {
                        try
                        {
                            Debug.WriteLine(">> waiting for connection");
                            await _connection.WaitForMeadowAttach();
                            Debug.WriteLine(">> delay");
                            await Task.Delay(3000); // wait to allow full boot - no idea why this takes longer

                            if (info == null)
                            {
                                Debug.WriteLine(">> query device info");
                                info = await _connection.Device.GetDeviceInfo();
                            }

                            Debug.WriteLine(">> flashing ESP");
                            await _connection.Device.FlashCoprocessor(RequestedVersion);
                            //                            await _connection.Device.FlashCoprocessor(DownloadManager.FirmwareDownloadsFilePath, RequestedVersion);
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
                        await _connection.Device.Reset();
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
                        await _connection.WaitForMeadowAttach();
                        await Task.Delay(2000); // wait 2 seconds to allow full boot
                        info = await _connection.Device.GetDeviceInfo();
                        if (info.OsVersion != RequestedVersion)
                        {
                            // this is a failure
                            _logger?.LogWarning($"OS version {info.OsVersion} does not match requested version {RequestedVersion}");
                        }
                        if (info.RuntimeVersion != RequestedVersion)
                        {
                            // this is a failure
                            _logger?.LogWarning($"Runtime version {info.RuntimeVersion} does not match requested version {RequestedVersion}");
                        }
                        if (info.CoprocessorOsVersion != RequestedVersion)
                        {
                            // not necessarily an error
                            _logger?.LogWarning($"Coprocessor version {info.CoprocessorOsVersion} does not match requested version {RequestedVersion}");
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
                    _logger?.LogInformation("Update complete");
                    return;
                default:
                    break;
            }

            await Task.Delay(1000);
        }
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
            CurrentState = UpdateState.NotStarted;
        }

        return Task.Run(StateMachine);
    }
}
