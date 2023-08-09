namespace Meadow.Hcom
{
    public partial class MeadowDevice : IMeadowDevice
    {
        private IMeadowConnection _connection;
        private ResponseListener _listener;
        private Exception? _lastException;

        public int CommandTimeoutSeconds { get; set; } = 30;

        internal MeadowDevice(IMeadowConnection connection)
        {
            _connection = connection;
            _connection.AddListener(_listener = new ResponseListener());
            _connection.ConnectionError += (s, e) => _lastException = e;
        }

        private async Task<bool> WaitForResult(Func<bool> checkAction, CancellationToken? cancellationToken)
        {
            var timeout = CommandTimeoutSeconds * 2;

            while (timeout-- > 0)
            {
                if (cancellationToken?.IsCancellationRequested ?? false) return false;
                if (_lastException != null) return false;

                if (timeout <= 0) throw new TimeoutException();

                if (checkAction())
                {
                    break;
                }

                await Task.Delay(500);
            }

            return true;
        }

        public async Task<bool> IsRuntimeEnabled(CancellationToken? cancellationToken = null)
        {
            var command = RequestBuilder.Build<GetRuntimeStateRequest>();

            _listener.Information.Clear();

            _connection.EnqueueRequest(command);

            // wait for an information response
            var timeout = CommandTimeoutSeconds * 2;
            while (timeout-- > 0)
            {
                if (cancellationToken?.IsCancellationRequested ?? false) return false;
                if (timeout <= 0) throw new TimeoutException();

                if (_listener.Information.Count > 0)
                {
                    var m = _listener.Information.FirstOrDefault(i => i.Contains("Mono is"));
                    if (m != null)
                    {
                        return m == "Mono is enabled";
                    }
                }

                await Task.Delay(500);
            }
            return false;
        }

        public async Task Reset(CancellationToken? cancellationToken = null)
        {
            var command = RequestBuilder.Build<ResetDeviceRequest>();

            _connection.EnqueueRequest(command);

            // we have to give time for the device to actually reset
            await Task.Delay(500);

            await _connection.WaitForMeadowAttach(cancellationToken);
        }

        public async Task RuntimeDisable(CancellationToken? cancellationToken = null)
        {
            var command = RequestBuilder.Build<RuntimeDisableRequest>();

            _listener.Information.Clear();

            _connection.EnqueueRequest(command);

            // we have to give time for the device to actually reset
            await Task.Delay(500);

            var success = await WaitForResult(() =>
            {
                if (_listener.Information.Count > 0)
                {
                    var m = _listener.Information.FirstOrDefault(i => i.Contains("Mono is disabled"));
                    if (m != null)
                    {
                        return true;
                    }
                }

                return false;
            }, cancellationToken);

            if (!success) throw new Exception("Unable to disable runtime");
        }

        public async Task RuntimeEnable(CancellationToken? cancellationToken = null)
        {
            var command = RequestBuilder.Build<RuntimeEnableRequest>();

            _listener.Information.Clear();

            _connection.EnqueueRequest(command);

            // we have to give time for the device to actually reset
            await Task.Delay(500);

            var success = await WaitForResult(() =>
            {
                if (_listener.Information.Count > 0)
                {
                    var m = _listener.Information.FirstOrDefault(i => i.Contains("Meadow successfully started MONO"));
                    if (m != null)
                    {
                        return true;
                    }
                }

                return false;
            }, cancellationToken);

            if (!success) throw new Exception("Unable to enable runtime");
        }

        public async Task<DeviceInfo?> GetDeviceInfo(CancellationToken? cancellationToken = null)
        {
            var command = RequestBuilder.Build<GetDeviceInfoRequest>();

            _listener.DeviceInfo.Clear();

            _lastException = null;
            _connection.EnqueueRequest(command);

            if (!await WaitForResult(
                () => _listener.DeviceInfo.Count > 0,
                cancellationToken))
            {
                return null;
            }

            return new DeviceInfo(_listener.DeviceInfo);
        }

        public async Task<MeadowFileInfo[]?> GetFileList(bool includeCrcs, CancellationToken? cancellationToken = null)
        {
            var command = RequestBuilder.Build<GetFileListRequest>();
            command.IncludeCrcs = includeCrcs;

            _listener.DeviceInfo.Clear();

            _connection.EnqueueRequest(command);

            if (!await WaitForResult(
                () => _listener.TextList.Count > 0,
                cancellationToken))
            {
                return null;
            }

            var list = new List<MeadowFileInfo>();

            foreach (var candidate in _listener.TextList)
            {
                // TODO: should this be part of the connection?  A serial response might be different than a future response (TCP or whatever)
                var fi = MeadowFileInfo.Parse(candidate);
                if (fi != null)
                {
                    list.Add(fi);
                }
            }

            return list.ToArray();

        }

        public async Task<bool> ReadFile(string meadowFileName, string? localFileName = null, CancellationToken? cancellationToken = null)
        {
            var command = RequestBuilder.Build<InitFileReadRequest>();
            command.MeadowFileName = meadowFileName;
            command.LocalFileName = localFileName;

            var completed = false;
            Exception? ex = null;

            void OnFileReadCompleted(object? sender, string filename)
            {
                completed = true;
            }
            void OnFileError(object? sender, Exception exception)
            {
                ex = exception;
            }

            try
            {
                _connection.FileReadCompleted += OnFileReadCompleted;
                _connection.FileException += OnFileError;

                _connection.EnqueueRequest(command);

                if (!await WaitForResult(
                    () =>
                    {
                        if (ex != null) throw ex;
                        return completed;
                    },
                    cancellationToken))
                {
                    return false;
                }

                return true;
            }
            finally
            {
                _connection.FileReadCompleted -= OnFileReadCompleted;
                _connection.FileException -= OnFileError;
            }
        }

        public async Task<bool> WriteFile(string localFileName, string? meadowFileName = null, CancellationToken? cancellationToken = null)
        {
            var command = RequestBuilder.Build<InitFileWriteRequest>();
            command.LocalFileName = localFileName;
            command.MeadowFileName = meadowFileName;

            _connection.EnqueueRequest(command);
            return false;
        }

        public Task FlashOS(string requestedversion, CancellationToken? cancellationToken = null)
        {
            throw new NotImplementedException();
        }

        public Task FlashCoprocessor(string requestedversion, CancellationToken? cancellationToken = null)
        {
            throw new NotImplementedException();
        }

        public Task FlashRuntime(string requestedversion, CancellationToken? cancellationToken = null)
        {
            throw new NotImplementedException();
        }

        public async Task<DateTimeOffset?> GetRtcTime(CancellationToken? cancellationToken = null)
        {
            var command = RequestBuilder.Build<GetRtcTimeRequest>();

            _listener.Information.Clear();

            _connection.EnqueueRequest(command);

            DateTimeOffset? now = null;

            var success = await WaitForResult(() =>
            {
                var token = "UTC time:";

                if (_listener.Information.Count > 0)
                {
                    var m = _listener.Information.FirstOrDefault(i => i.Contains(token));
                    if (m != null)
                    {
                        var timeString = m.Substring(m.IndexOf(token) + token.Length);
                        now = DateTimeOffset.Parse(timeString);
                        return true;
                    }
                }

                return false;
            }, cancellationToken);

            return now;
        }

        public async Task SetRtcTime(DateTimeOffset dateTime, CancellationToken? cancellationToken = null)
        {
            var command = RequestBuilder.Build<SetRtcTimeRequest>();
            command.Time = dateTime;

            _listener.LastRequestConcluded = null;

            _connection.EnqueueRequest(command);

            var success = await WaitForResult(() =>
            {
                if (_listener.LastRequestConcluded != null && _listener.LastRequestConcluded == 0x303)
                {
                    return true;
                }

                return false;
            }, cancellationToken);
        }
    }
}