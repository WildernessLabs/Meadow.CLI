using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Meadow.Hcom
{
    public partial class SerialConnection
    {
        private bool _reconnectInProgress = false;

        public event EventHandler<Exception> FileException = delegate { };

        public async Task WaitForMeadowAttach(CancellationToken? cancellationToken)
        {
            var timeout = 20;

            while (timeout-- > 0)
            {
                if (cancellationToken?.IsCancellationRequested ?? false) throw new TaskCanceledException();
                if (timeout <= 0) throw new TimeoutException();

                if (State == ConnectionState.MeadowAttached) return;

                await Task.Delay(500);
            }

            throw new TimeoutException();
        }

        private async Task ListenerProc()
        {
            var readBuffer = new byte[ReadBufferSizeBytes];
            var decodedBuffer = new byte[8192];
            var messageBytes = new CircularBuffer<byte>(8192 * 2);
            var delimiter = new byte[] { 0x00 };

            while (!_isDisposed)
            {
                if (_port.IsOpen)
                {
                    try
                    {
                        Debug.WriteLine($"listening...");

                        var receivedLength = _port.BaseStream.Read(readBuffer, 0, readBuffer.Length);

                        Debug.WriteLine($"Received {receivedLength} bytes");

                        if (receivedLength > 0)
                        {
                            messageBytes.Append(readBuffer, 0, receivedLength);

                            while (messageBytes.Count > 0)
                            {
                                var index = messageBytes.FirstIndexOf(delimiter);

                                if (index < 0)
                                {
                                    Debug.WriteLine($"No delimiter");
                                    break;
                                }
                                var packetBytes = messageBytes.Remove(index + 1);

                                if (packetBytes.Length == 1)
                                {
                                    // It's possible that we may find a series of 0x00 values in the buffer.
                                    // This is because when the sender is blocked (because this code isn't
                                    // running) it will attempt to send a single 0x00 before the full message.
                                    // This allows it to test for a connection. When the connection is
                                    // unblocked this 0x00 is sent and gets put into the buffer along with
                                    // any others that were queued along the usb serial pipe line.

                                    // we discard this single 0x00 byte
                                }
                                else
                                {
                                    Debug.WriteLine($"Received a {packetBytes.Length} byte packet");

                                    var decodedSize = CobsTools.CobsDecoding(packetBytes, packetBytes.Length - delimiter.Length, ref decodedBuffer);

                                    // now parse this per the HCOM protocol definition
                                    var response = SerialResponse.Parse(decodedBuffer, decodedSize);

                                    Debug.WriteLine($"{response.RequestType}");
                                    _state = ConnectionState.MeadowAttached;

                                    if (response != null)
                                    {
                                        _messageCount++;
                                    }

                                    if (response is TextInformationResponse tir)
                                    {
                                        // send the message to any listeners
                                        Debug.WriteLine($"INFO> {tir.Text}");

                                        foreach (var listener in _listeners)
                                        {
                                            listener.OnInformationMessageReceived(tir.Text);
                                        }
                                    }
                                    else if (response is TextStdOutResponse tso)
                                    {
                                        // send the message to any listeners
                                        Debug.WriteLine($"STDOUT> {tso.Text}");

                                        foreach (var listener in _listeners)
                                        {
                                            listener.OnStdOutReceived(tso.Text);
                                        }
                                    }
                                    else if (response is TextStdErrResponse tse)
                                    {
                                        // send the message to any listeners
                                        Debug.WriteLine($"STDERR> {tse.Text}");

                                        foreach (var listener in _listeners)
                                        {
                                            listener.OnStdErrReceived(tse.Text);
                                        }
                                    }
                                    else if (response is TextListHeaderResponse tlh)
                                    {
                                        // start of a list
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
                                        foreach (var listener in _listeners)
                                        {
                                            listener.OnTextMessageConcluded((int)tcr.RequestType);
                                        }

                                        if (_reconnectInProgress)
                                        {
                                            _state = ConnectionState.MeadowAttached;
                                            _reconnectInProgress = false;
                                        }
                                        else
                                        {
                                            foreach (var listener in _listeners)
                                            {
                                                listener.OnTextListReceived(_textList.ToArray());
                                            }
                                        }
                                    }
                                    else if (response is TextRequestResponse trr)
                                    {
                                        // this is a response to a text request - the exact request is cached
                                        Debug.WriteLine($"RESPONSE> {trr.Text}");
                                    }
                                    else if (response is DeviceInfoSerialResponse dir)
                                    {
                                        foreach (var listener in _listeners)
                                        {
                                            listener.OnDeviceInformationMessageReceived(dir.Fields);
                                        }
                                    }
                                    else if (response is ReconnectRequiredResponse rrr)
                                    {
                                        // the device is going to restart - we need to wait for a HCOM_HOST_REQUEST_TEXT_CONCLUDED to know it's back
                                        _state = ConnectionState.Disconnected;
                                        _reconnectInProgress = true;
                                    }
                                    else if (response is FileReadInitOkResponse fri)
                                    {
                                        // Once HCOM_MDOW_REQUEST_UPLOAD_FILE_INIT is sent the F7 will respond
                                        // with either HCOM_HOST_REQUEST_INIT_UPLOAD_OKAY or
                                        // HCOM_HOST_REQUEST_INIT_UPLOAD_FAIL.
                                        //
                                        // If we get HCOM_HOST_REQUEST_INIT_UPLOAD_OKAY we must open a file on
                                        // this machine and respond with HCOM_MDOW_REQUEST_UPLOAD_READY_SEND_DATA.
                                        //
                                        // The F7 will begin to send HCOM_HOST_REQUEST_UPLOADING_FILE_DATA which
                                        // contains the file data, which we must write to the open file.
                                        //
                                        // When the F7 has finished sending the data it will send a
                                        // HCOM_HOST_REQUEST_UPLOAD_FILE_COMPLETED message. When it is received
                                        // we then close the open file and the process is completed.
                                        var folder = Path.GetDirectoryName(_readFileInfo.LocalFileName);
                                        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);

                                        _readFileInfo.FileStream = File.Create(_readFileInfo.LocalFileName);

                                        var uploadRequest = RequestBuilder.Build<StartFileDataRequest>();

                                        (this as IMeadowConnection).EnqueueRequest(uploadRequest);
                                    }
                                    else if (response is UploadDataPacketResponse udp)
                                    {
                                        if (_readFileInfo == null)
                                        {
                                            throw new Exception("Data received for unknown file");
                                        }

                                        _readFileInfo.FileStream.Write(udp.FileData);
                                    }
                                    else if (response is UploadCompletedResponse ucr)
                                    {
                                        if (_readFileInfo == null)
                                        {
                                            throw new Exception("File Complete received for unknown file");
                                        }

                                        var fn = _readFileInfo.LocalFileName;

                                        _readFileInfo.FileStream.Flush();
                                        _readFileInfo.FileStream.Dispose();
                                        _readFileInfo = null;

                                        FileReadCompleted?.Invoke(this, fn);
                                    }
                                    else if (response is FileReadInitFailedResponse frf)
                                    {
                                        _readFileInfo = null;
                                        throw new Exception(_lastError ?? "unknown error");
                                    }
                                    else if (response is RequestErrorTextResponse ret)
                                    {
                                        _lastError = ret.Text;
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"{response.GetType().Name} for:{response.RequestType}");
                                        // try to match responses with the requests
                                    }
                                }
                            }
                        }
                    }
                    catch (DirectoryNotFoundException dnf)
                    {
                        FileException?.Invoke(this, dnf);
                    }
                    catch (IOException)
                    {
                        // attempt to read timed out (i.e. there's just no data)
                        // NOP
                    }
                    catch (TimeoutException)
                    {
                        Debug.WriteLine($"listen timeout");
                    }
                    catch (ThreadAbortException)
                    {
                        //ignoring for now until we wire cancellation ...
                        //this blocks the thread abort exception when the console app closes
                        Debug.WriteLine($"listen abort");
                    }
                    catch (InvalidOperationException)
                    {
                        // common if the port is reset/closed (e.g. mono enable/disable) - don't spew confusing info
                        Debug.WriteLine($"listen on closed port");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"listen error {ex.Message}");
                        _logger?.LogTrace(ex, "An error occurred while listening to the serial port.");
                        await Task.Delay(1000);
                    }
                }
                else
                {
                    await Task.Delay(500);
                }
            }
        }
    }
}