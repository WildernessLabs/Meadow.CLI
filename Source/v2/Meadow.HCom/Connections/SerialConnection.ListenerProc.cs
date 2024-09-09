﻿namespace Meadow.Hcom
{
    public partial class SerialConnection
    {
        private bool _reconnectInProgress = false;

        public event EventHandler<Exception> FileException = delegate { };

        private async Task ListenerProc()
        {
            var readBuffer = new byte[ReadBufferSizeBytes];
            var decodedBuffer = new byte[8192];
            var messageBytes = new CircularBuffer<byte>(8192 * 2);
            var delimiter = new byte[] { 0x00 };
            var receivedLength = 0;

            while (!_isDisposed)
            {
                if (_port.IsOpen)
                {
                    try
                    {
                        Debug.WriteLine($"listening...");

                    read:
                        try
                        {
                            receivedLength = _port.Read(readBuffer, 0, readBuffer.Length);
                        }
                        catch (OperationCanceledException)
                        {
                            Debug.WriteLine($"Device reset detected");

                            var timeout = 20;

                            while (!_port.IsOpen)
                            {
                                await Task.Delay(500);

                                if (timeout-- < 0)
                                {
                                    return;
                                }

                                try
                                {
                                    Open();
                                    Debug.WriteLine($"Port re-opened");
                                }
                                catch
                                {
                                    Debug.WriteLine($"Failed to re-open port");
                                }
                            }
                            goto read;
                        }

                        Debug.WriteLine($"Received {receivedLength} bytes");

                        if (receivedLength > 0)
                        {
                            messageBytes.Append(readBuffer, 0, receivedLength);

                            while (messageBytes.Count > 0)
                            {
                                var index = messageBytes.FirstIndexOf(delimiter);

                                if (index < 0)
                                {
                                    if (messageBytes.Count > 0)
                                    {
                                        Debug.WriteLine($"We have {messageBytes.Count} bytes with no end delimiter");
                                    }
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

                                    await ParseResponse(response);
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
                    catch (OperationCanceledException)
                    {
                        // this happens on disconnect - could be cable pulled, could be device reset
                        Debug.WriteLine($"Operation Cancelled");
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
                    await Task.Delay(500);
                }
            }
        }

        private async Task ParseResponse(SerialResponse? response)
        {
            // If possible, consider keeping thes responses in alphabetical order,
            // so it's easier to find them.

            switch (response)
            {
                case DebuggingDataResponse ddr:
                    await RaiseDebuggerMessage(ddr.Data);
                    break;

                case DeviceInfoSerialResponse dir:
                    _deviceInfo = new DeviceInfo(dir.Fields);
                    break;

                case FileDownloadFailedResponse fdf:
                    RaiseFileWriteFailed();
                    break;

                case FileReadInitFailedResponse frf:
                    _readFileInfo = null;
                    throw new Exception(_lastError ?? "unknown error");

                case FileWriteInitFailedSerialResponse fwf:
                    _readFileInfo = null;
                    FileException?.Invoke(this, new Exception(_lastError ?? "unknown error"));
                    break;

                case FileReadInitOkResponse fri:
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
                    var folder = Path.GetDirectoryName(_readFileInfo!.LocalFileName);
                    if (!Directory.Exists(folder)) throw new DirectoryNotFoundException(folder);

                    _readFileInfo.FileStream = File.Create(_readFileInfo.LocalFileName);

                    var uploadRequest = RequestBuilder.Build<StartFileDataRequest>();
                    EncodeAndSendPacket(uploadRequest.Serialize());
                    break;

                case FileWriteInitOkSerialResponse fws:
                    FileWriteAccepted?.Invoke(this, EventArgs.Empty);
                    break;

                case ReconnectRequiredResponse rrr:
                    // the device is going to restart - we need to wait for a HCOM_HOST_REQUEST_TEXT_CONCLUDED to know it's back
                    Close();

                    await Task.Delay(3000);

                    Open();
                    break;

                case RequestErrorTextResponse ret:
                    Debug.WriteLine(ret.Text);
                    RaiseDeviceMessageReceived(ret.Text, "hcom");
                    _lastError = ret.Text;
                    break;

                case TextCrcMemberResponse tcm:
                    _textList.Add(tcm.Text);
                    break;

                case TextConcludedResponse tcr:
                    _lastRequestConcluded = (RequestType)tcr.RequestType;

                    if (_reconnectInProgress)
                    {
                        Open();
                        _reconnectInProgress = false;
                    }
                    else if (_textListComplete != null)
                    {
                        _textListComplete = true;
                    }
                    break;

                case TextInformationResponse tir:
                    // send the message to any listeners
                    Debug.WriteLine($"INFO> {tir.Text}");

                    InfoMessages.Add(tir.Text);
                    base.RaiseDeviceMessageReceived(tir.Text, "info");
                    break;

                case TextListHeaderResponse tlh:
                    // start of a list
                    _textListComplete = false;
                    _textList.Clear();
                    break;

                case TextListMemberResponse tlm:
                    _textList.Add(tlm.Text);
                    break;

                case TextPayloadSerialResponse tps:
                    FileTextReceived?.Invoke(this, tps.Text);
                    break;

                case TextRequestResponse trr:
                    // this is a response to a text request - the exact request is cached
                    //Debug.WriteLine($"RESPONSE> {trr.Text}");
                    break;

                case TextStdErrResponse tse:
                    // send the message to any listeners
                    Debug.WriteLine($"STDERR> {tse.Text}");

                    StdErr.Add(tse.Text);
                    base.RaiseDeviceMessageReceived(tse.Text, "stderr");
                    break;

                case TextStdOutResponse tso:
                    Debug.WriteLine($"STDOUT> {tso.Text}");

                    StdOut.Add(tso.Text);
                    base.RaiseDeviceMessageReceived(tso.Text, "stdout");
                    break;

                case UploadCompletedResponse ucr:
                    if (_readFileInfo == null)
                    {
                        throw new Exception("File Complete received for unknown file");
                    }

                    var fn = _readFileInfo.LocalFileName;

                    _readFileInfo.FileStream.Flush();
                    _readFileInfo.FileStream.Dispose();
                    _readFileInfo = null;

                    RaiseFileReadCompleted(fn ?? string.Empty);
                    break;

                case UploadDataPacketResponse udp:
                    if (_readFileInfo == null)
                    {
                        throw new Exception("Data received for unknown file");
                    }

                    _readFileInfo.FileStream.Write(udp.FileData, 0, udp.FileData.Length);

                    RaiseFileBytesReceived(udp.FileData.Length);
                    break;

                default:
                    Debug.WriteLine($"{response?.GetType().Name} for: {response?.RequestType}");
                    // try to match responses with the requests
                    break;
            };
        }
    }
}