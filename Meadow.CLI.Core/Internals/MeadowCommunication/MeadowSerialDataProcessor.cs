using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.DeviceManagement.Tools;
using Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses;
using Meadow.CLI.Core.Logging;

namespace Meadow.CLI.Core.Internals.MeadowCommunication
{
    // For data received due to a CLI request these provide a secondary
    // type of identification. The primary being the protocol request value
    public enum MeadowMessageType
    {
        AppOutput,
        ErrOutput,
        DeviceInfo,
        FileListTitle,
        FileListMember,
        FileListCrcMember,
        Data,
        InitialFileData,
        MeadowTrace,
        SerialReconnect,
        Accepted,
        Concluded,
        DownloadStartOkay,
        DownloadStartFail,
    }

    public class MeadowSerialDataProcessor : MeadowDataProcessor
    {
        private readonly IMeadowLogger _logger;
        //collapse to one and use enum
        private readonly SerialPort _serialPort;
        readonly Socket _socket;
        private readonly Task _dataProcessorTask;

        private readonly ReceiveMessageFactoryManager _receiveMessageFactoryManager;
        private readonly CancellationTokenSource _cts;

        // It seems that the .Net SerialPort class is not all it could be.
        // To acheive reliable operation some SerialPort class methods must
        // not be used. When receiving, the BaseStream must be used.
        // http://www.sparxeng.com/blog/software/must-use-net-system-io-ports-serialport

        //-------------------------------------------------------------
        // Constructor
        private MeadowSerialDataProcessor(IMeadowLogger logger)
        {
            _cts = new CancellationTokenSource();
            _receiveMessageFactoryManager = new ReceiveMessageFactoryManager(logger);
            _logger = logger;
        }

        public MeadowSerialDataProcessor(SerialPort serialPort, IMeadowLogger? logger = null) : this(logger ?? new NullMeadowLogger<MeadowSerialDataProcessor>())
        {
            _serialPort = serialPort;
            _dataProcessorTask = Task.Factory.StartNew(ReadSerialPortAsync, TaskCreationOptions.LongRunning);
        }

        public MeadowSerialDataProcessor(Socket socket, IMeadowLogger? logger = null) : this(logger ?? new NullMeadowLogger<MeadowSerialDataProcessor>())
        {
            this._socket = socket;
            _dataProcessorTask = Task.Factory.StartNew(ReadSocketAsync, TaskCreationOptions.LongRunning);
        }

        //-------------------------------------------------------------
        // All received data handled here
        private async Task ReadSocketAsync()
        {
            byte[] buffer = new byte[MeadowDeviceManager.MaxEstimatedSizeOfEncodedPayload];

            try
            {
                while (true)
                {
                    var segment = new ArraySegment<byte>(buffer);
                    var receivedLength = await _socket.ReceiveAsync(segment, SocketFlags.None).ConfigureAwait(false);

                    DecodeAndProcessPacket(buffer.AsMemory(0, receivedLength), _cts.Token);

                    await Task.Delay(50).ConfigureAwait(false);
                }
            }
            catch (ThreadAbortException)
            {
                //ignoring for now until we wire cancellation ...
                //this blocks the thread abort exception when the console app closes
            }
            catch (InvalidOperationException)
            {
                // common if the port is reset/closed (e.g. mono enable/disable) - don't spew confusing info
            }
            catch (Exception ex)
            {
                _logger.LogTrace($"Exception: {ex} may mean the target connection dropped");
            }
        }

        private class SerialMessage
        {
            private readonly IList<Memory<byte>> _segments;

            public SerialMessage(Memory<byte> segment)
            {
                _segments = new List<Memory<byte>>();
                _segments.Add(segment);
            }

            public void AddSegment(Memory<byte> segment)
            {
                _segments.Add(segment);
            }

            public byte[] ToArray()
            {
                using var ms = new MemoryStream();
                foreach (var segment in _segments)
                {
                    // We could just call ToArray on the `Memory` but that will result in an uncontrolled allocation.
                    var tmp = ArrayPool<byte>.Shared.Rent(segment.Length);
                    segment.CopyTo(tmp);
                    ms.Write(tmp, 0, segment.Length);
                    ArrayPool<byte>.Shared.Return(tmp);
                }
                return ms.ToArray();
            }
        }

        private async Task ReadSerialPortAsync()
        {
            SerialMessage? message = null;
            try
            {
                while (true)
                {
                    try
                    {
                        // Reconnection happens higher up
                        while (!_serialPort.IsOpen)
                        {
                            await Task.Delay(100)
                                      .ConfigureAwait(false);
                        }

                        var b = new byte[1024];
                        var receivedLength = await _serialPort.BaseStream.ReadAsync(b, 0, b.Length)
                                                              .ConfigureAwait(false);

                        var buffer = b.AsMemory(0, receivedLength);
                        while (buffer.Length > 0)
                        {
                            var messageEnd = buffer.Span.IndexOf((byte)0x00);
                            // We didn't find the end to a message
                            if (messageEnd == -1)
                            {
                                if (message == null)
                                    message = new SerialMessage(buffer);
                                else
                                    message.AddSegment(buffer);

                                break;
                            }
                            else
                            {
                                // We read the whole message during this iteration
                                if (message == null)
                                {
                                    var msg = buffer.Slice(0, messageEnd);
                                    buffer = buffer.Slice(messageEnd + 1);
                                    DecodeAndProcessPacket(msg, _cts.Token);
                                }
                                // We had some part of the message from a previous iteration
                                else
                                {
                                    message.AddSegment(buffer.Slice(0,messageEnd));
                                    buffer = buffer.Slice(messageEnd + 1);
                                    var msg = message.ToArray();
                                    DecodeAndProcessPacket(msg, _cts.Token);

                                    message = null;
                                }
                            }
                        }
                    }
                    catch (TimeoutException ex)
                    {
                    }
                    catch (Exception ex)
                    {
                        _logger.LogTrace(ex, "An error occurred while listening to the serial port.");
                        await Task.Delay(100, _cts.Token)
                                  .ConfigureAwait(false);
                    }
                }
            }
            catch (ThreadAbortException)
            {
                //ignoring for now until we wire cancellation ...
                //this blocks the thread abort exception when the console app closes
            }
            catch (InvalidOperationException)
            {
                // common if the port is reset/closed (e.g. mono enable/disable) - don't spew confusing info
            }
            catch (Exception ex)
            {
                _logger.LogTrace($"Exception: {ex} may mean the target connection dropped");
            }
        }

        private bool DecodeAndProcessPacket(Memory<byte> packetBuffer, CancellationToken cancellationToken)
        {
            var decodedBuffer = ArrayPool<byte>.Shared.Rent(MeadowDeviceManager.MaxAllowableMsgPacketLength);
            var packetLength = packetBuffer.Length;
            // It's possible that we may find a series of 0x00 values in the buffer.
            // This is because when the sender is blocked (because this code isn't
            // running) it will attempt to send a single 0x00 before the full message.
            // This allows it to test for a connection. When the connection is
            // unblocked this 0x00 is sent and gets put into the buffer along with
            // any others that were queued along the usb serial pipe line.
            if (packetLength == 1)
            {
                //_logger.LogTrace("Throwing out 0x00 from buffer");
                return false;
            }

            var decodedSize = CobsTools.CobsDecoding(packetBuffer, ref decodedBuffer);

            // If a message is too short it is ignored
            if (decodedSize < MeadowDeviceManager.ProtocolHeaderSize)
                return false;

            Debug.Assert(decodedSize <= MeadowDeviceManager.MaxAllowableMsgPacketLength);

            // Process the received packet
            ParseAndProcessReceivedPacket(decodedBuffer.AsSpan(0, decodedSize).ToArray(),
                                          cancellationToken);

            ArrayPool<byte>.Shared.Return(decodedBuffer);
            return true;
        }

        // TODO: Convert to Memory<byte> from byte[]
        private void ParseAndProcessReceivedPacket(byte[] receivedMsg, CancellationToken cancellationToken)
        {
            try
            {
                var processor = _receiveMessageFactoryManager.CreateProcessor(receivedMsg);
                if (processor == null) return;

                if (processor.Execute(receivedMsg))
                {
                    var requestType = (HcomHostRequestType)processor.RequestType;
                    var responseString = processor.ToString();
                    _logger.LogTrace("Received message {messageType}, Content: {messageContent}", requestType, responseString);
                    switch (requestType)
                    {
                        case HcomHostRequestType.HCOM_HOST_REQUEST_UNDEFINED_REQUEST:
                            break;
                        // This set are responses to request issued by this application
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_REJECTED:
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.Data, responseString));
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_ACCEPTED:
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.Accepted));
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_CONCLUDED:
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.Concluded));
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_ERROR:
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.Data, responseString));
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_INFORMATION:
                            _logger.LogInformation("Meadow StdInfo: {message}", responseString);
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.Data, responseString));
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_LIST_HEADER:
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.FileListTitle, responseString));
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_LIST_MEMBER:
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.FileListMember, responseString));
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_CRC_MEMBER:
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.FileListCrcMember, responseString));
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_MONO_STDOUT:
                            _logger.LogInformation("Meadow StdOut: {message}", responseString);
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.AppOutput, responseString));
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_MONO_STDERR:
                            _logger.LogWarning("Meadow StdErr: {message}", responseString);
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.ErrOutput, responseString));
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_DEVICE_INFO:
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.DeviceInfo, responseString));
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_TRACE_MSG:
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.MeadowTrace, responseString));
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_RECONNECT:
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.SerialReconnect));
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_DEBUGGING_MONO_DATA:
                            DebuggerMessages.Add(processor.MessageData!, cancellationToken);
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_FILE_START_OKAY:
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.DownloadStartOkay));
                            break;

                        case HcomHostRequestType.HCOM_HOST_REQUEST_FILE_START_FAIL:
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.DownloadStartFail));
                            break;

                        case HcomHostRequestType.HCOM_HOST_REQUEST_GET_INITIAL_FILE_BYTES:
                        {
                            var msg = System.Text.Encoding.UTF8.GetString(processor.MessageData);

                            OnReceiveData?.Invoke(
                                this,
                                new MeadowMessageEventArgs(MeadowMessageType.InitialFileData, msg));

                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "An error occurred parsing a received packet");
            }
        }

        /*
        // Save for testing in case we suspect data corruption of text
        // The protocol requires the first 12 bytes to be the header. The first 2 are 0x00,
        // the next 10 are binary. After this the rest are ASCII text or binary.
        // Test the message and if it fails it's trashed.
        if(decodedBuffer[0] != 0x00 || decodedBuffer[1] != 0x00)
        {
            _logger.LogTrace("Corrupted message, first 2 bytes not 0x00");
            continue;
        }

        int buffOffset;
        for(buffOffset = MeadowDeviceManager.HCOM_PROTOCOL_COMMAND_REQUIRED_HEADER_LENGTH;
            buffOffset < decodedSize;
            buffOffset++)
        {
            if(decodedBuffer[buffOffset] < 0x20 || decodedBuffer[buffOffset] > 0x7e)
            {
                _logger.LogTrace($"Corrupted message, non-ascii at offset:{buffOffset} value:{decodedBuffer[buffOffset]}");
                break;
            }
        }

        // Throw away if we found non ASCII where only text should be
        if (buffOffset < decodedSize)
            continue;
        */
        public override void Dispose()
        {
            try
            {
                _cts.Cancel();
                //_pipeReaderTask?.Dispose();
                //_pipeWriterTask?.Dispose();
                _dataProcessorTask?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Exception during disposal");
            }
        }
    }
}