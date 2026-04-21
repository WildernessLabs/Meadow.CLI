using System.Net.Sockets;

namespace Meadow.Hcom
{
    public partial class SocketConnection
    {
        public event EventHandler<Exception> FileException = delegate { };

        public override async Task WaitForMeadowAttach(CancellationToken? cancellationToken)
        {
            var timeout = 500;

            while (timeout-- > 0)
            {
                if (cancellationToken?.IsCancellationRequested ?? false) throw new TaskCanceledException();
                if (timeout <= 0) throw new TimeoutException();

                if (State == ConnectionState.MeadowAttached)
                {
                    if (Device == null)
                    {
                        await Attach(cancellationToken, 5);
                    }

                    return;
                }

                await Task.Delay(20);

                if (!IsConnected)
                {
                    try
                    {
                        Open();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Unable to connect: {ex.Message}");
                    }
                }
            }

            throw new TimeoutException();
        }

        private async Task ListenerProc()
        {
            var readBuffer = new byte[ReadBufferSizeBytes];
            var decodedBuffer = new byte[8192];
            var messageBytes = new List<byte>();
            var receivedLength = 0;

            while (!_isDisposed)
            {
                if (IsConnected && _networkStream != null)
                {
                    try
                    {
                        Debug.WriteLine($"listening...");

                        try
                        {
                            receivedLength = _networkStream.Read(readBuffer, 0, readBuffer.Length);
                        }
                        catch (IOException)
                        {
                            // read timeout or connection reset
                            continue;
                        }
                        catch (ObjectDisposedException)
                        {
                            continue;
                        }

                        if (receivedLength == 0)
                        {
                            // TCP connection closed by remote
                            Debug.WriteLine("Remote connection closed");
                            Close();
                            continue;
                        }

                        Debug.WriteLine($"Received {receivedLength} bytes");

                        if (receivedLength > 0)
                        {
                            for (var i = 0; i < receivedLength; i++) messageBytes.Add(readBuffer[i]);

                            while (messageBytes.Count > 0)
                            {
                                var index = messageBytes.IndexOf((byte)0x00);

                                if (index < 0)
                                {
                                    if (messageBytes.Count > 0)
                                    {
                                        Debug.WriteLine($"We have {messageBytes.Count} bytes with no end delimiter");
                                    }
                                    break;
                                }
                                var packetCount = index + 1;
                                var packetBytes = new byte[packetCount];
                                messageBytes.CopyTo(0, packetBytes, 0, packetCount);
                                messageBytes.RemoveRange(0, packetCount);

                                if (packetBytes.Length == 1)
                                {
                                    // discard single 0x00 byte (connection test or start delimiter)
                                }
                                else
                                {
                                    Debug.WriteLine($"Received a {packetBytes.Length} byte packet");

                                    var decodedSize = CobsTools.CobsDecoding(packetBytes, packetBytes.Length - 1, ref decodedBuffer);

                                    var response = SerialResponse.Parse(decodedBuffer, decodedSize);

                                    if (response == null)
                                    {
                                        Debug.WriteLine($"Response parsing yielded null");
                                        continue;
                                    }

                                    Debug.WriteLine($"{response.RequestType}");
                                    _state = ConnectionState.MeadowAttached;

                                    if (response != null)
                                    {
                                        _messageCount++;
                                    }

                                    if (response is TextInformationResponse tir)
                                    {
                                        Debug.WriteLine($"INFO> {tir.Text}");

                                        InfoMessages.Add(tir.Text);
                                        base.RaiseDeviceMessageReceived(tir.Text, "info");
                                    }
                                    else if (response is TextStdOutResponse tso)
                                    {
                                        Debug.WriteLine($"STDOUT> {tso.Text}");

                                        StdOut.Add(tso.Text);
                                        base.RaiseDeviceMessageReceived(tso.Text, "stdout");
                                    }
                                    else if (response is TextStdErrResponse tse)
                                    {
                                        Debug.WriteLine($"STDERR> {tse.Text}");

                                        StdErr.Add(tse.Text);
                                        base.RaiseDeviceMessageReceived(tse.Text, "stderr");
                                    }
                                    else if (response is TextListHeaderResponse tlh)
                                    {
                                        _textListComplete = false;
                                        _textList.Clear();
                                    }
                                    else if (response is TextListMemberResponse tlm)
                                    {
                                        _textList.Add(tlm.Text);
                                    }
                                    else if (response is TextCrcMemberResponse tcm)
                                    {
                                        _textList.Add(tcm.Text);
                                    }
                                    else if (response is TextConcludedResponse tcr)
                                    {
                                        _lastRequestConcluded = (RequestType)tcr.RequestType;

                                        if (_textListComplete != null)
                                        {
                                            _textListComplete = true;
                                        }
                                    }
                                    else if (response is TextRequestResponse trr)
                                    {
                                        // response to a text request
                                    }
                                    else if (response is DeviceInfoSerialResponse dir)
                                    {
                                        _deviceInfo = new DeviceInfo(dir.Fields);
                                    }
                                    else if (response is ReconnectRequiredResponse rrr)
                                    {
                                        Close();

                                        await Task.Delay(3000);

                                        Open();

                                        // Signal that the preceding command completed — the device
                                        // restarted as requested. Without this, WaitForConcluded
                                        // never unblocks because the firmware doesn't send
                                        // TextConcluded after a reconnect-inducing command.
                                        _lastRequestConcluded = (RequestType)rrr.RequestType;
                                    }
                                    else if (response is FileReadInitOkResponse fri)
                                    {
                                        var folder = Path.GetDirectoryName(_readFileInfo!.LocalFileName);
                                        if (!Directory.Exists(folder))
                                        {
                                            Directory.CreateDirectory(folder!);
                                        }

                                        _readFileInfo.FileStream = File.Create(_readFileInfo.LocalFileName);
                                        _readFileInfo.ExpectedCrc = fri.UserData;

                                        var uploadRequest = RequestBuilder.Build<StartFileDataRequest>();
                                        EncodeAndSendPacket(uploadRequest.Serialize());
                                    }
                                    else if (response is UploadDataPacketResponse udp)
                                    {
                                        if (_readFileInfo == null)
                                        {
                                            throw new Exception("Data received for unknown file");
                                        }

                                        _readFileInfo.FileStream.Write(udp.FileData, 0, udp.FileData.Length);
                                        _readFileInfo.ActualCrc = NuttxCrc.Crc32part(udp.FileData, (uint)udp.FileData.Length, _readFileInfo.ActualCrc);

                                        RaiseFileBytesReceived(udp.FileData.Length);
                                    }
                                    else if (response is UploadCompletedResponse ucr)
                                    {
                                        if (_readFileInfo == null)
                                        {
                                            throw new Exception("File Complete received for unknown file");
                                        }

                                        var fn = _readFileInfo.LocalFileName;
                                        var expectedCrc = _readFileInfo.ExpectedCrc;
                                        var actualCrc = _readFileInfo.ActualCrc;

                                        _readFileInfo.FileStream.Flush();
                                        _readFileInfo.FileStream.Dispose();
                                        _readFileInfo = null;

                                        if (expectedCrc != 0 && actualCrc != expectedCrc)
                                        {
                                            File.Delete(fn);
                                            var crcError = new Exception($"File CRC mismatch: expected 0x{expectedCrc:X8}, received 0x{actualCrc:X8}");
                                            _logger?.LogError(crcError.Message);
                                            RaiseConnectionError(crcError);
                                        }

                                        RaiseFileReadCompleted(fn ?? string.Empty);
                                    }
                                    else if (response is FileReadInitFailedResponse frf)
                                    {
                                        _readFileInfo = null;
                                        throw new Exception(_lastError ?? "unknown error");
                                    }
                                    else if (response is RequestErrorTextResponse ret)
                                    {
                                        Debug.WriteLine(ret.Text);
                                        _lastError = ret.Text;
                                        RaiseDeviceMessageReceived(ret.Text, "error");
                                    }
                                    else if (response is FileWriteInitFailedSerialResponse fwf)
                                    {
                                        _readFileInfo = null;
                                        FileException?.Invoke(this, new Exception(_lastError ?? "unknown error"));
                                    }
                                    else if (response is FileWriteInitOkSerialResponse)
                                    {
                                        FileWriteAccepted?.Invoke(this, EventArgs.Empty);
                                    }
                                    else if (response is TextPayloadSerialResponse fib)
                                    {
                                        FileTextReceived?.Invoke(this, fib.Text);
                                    }
                                    else if (response is FileDownloadFailedResponse fdf)
                                    {
                                        RaiseFileWriteFailed();
                                    }
                                    else if (response is DebuggingDataResponse ddr)
                                    {
                                        RaiseDebuggerMessage(ddr.Data);
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"{response?.GetType().Name} for: {response?.RequestType}");
                                    }
                                }
                            }
                        }
                    }
                    catch (DirectoryNotFoundException dnf)
                    {
                        FileException?.Invoke(this, dnf);
                    }
                    catch (IOException ioe)
                    {
                        FileException?.Invoke(this, ioe);
                    }
                    catch (SocketException)
                    {
                        Debug.WriteLine($"Socket error - connection lost");
                        Close();
                    }
                    catch (TimeoutException)
                    {
                        Debug.WriteLine($"listen timeout");
                    }
                    catch (Exception ex)
                    {
                        RaiseConnectionError(ex);
                        Debug.WriteLine($"listen error {ex.Message}");
                        await Task.Delay(1000);
                    }
                }
                else
                {
                    // Not connected - try to connect
                    try
                    {
                        Open();
                    }
                    catch
                    {
                        Debug.WriteLine("Waiting for socket connection...");
                    }
                    await Task.Delay(500);
                }
            }
        }
    }
}
