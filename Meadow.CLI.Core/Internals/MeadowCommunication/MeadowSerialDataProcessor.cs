using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipelines;
using System.IO.Ports;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CLI.Core.DeviceManagement.Tools;
using Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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

    public class MeadowSerialDataProcessor : MeadowDataProcessor, IDisposable
    {
        private readonly ILogger _logger;
        //collapse to one and use enum
        private readonly SerialPort _serialPort;
        readonly Socket _socket;
        private readonly Task _dataProcessorTask;
        private readonly Task _pipeReaderTask;
        private readonly Task _pipeWriterTask;

        private readonly Pipe _pipe;
        private readonly ReceiveMessageFactoryManager _receiveMessageFactoryManager;
        private readonly CancellationTokenSource _cts;

        // It seems that the .Net SerialPort class is not all it could be.
        // To acheive reliable operation some SerialPort class methods must
        // not be used. When receiving, the BaseStream must be used.
        // http://www.sparxeng.com/blog/software/must-use-net-system-io-ports-serialport

        //-------------------------------------------------------------
        // Constructor
        private MeadowSerialDataProcessor(ILogger logger)
        {
            _cts = new CancellationTokenSource();
            _receiveMessageFactoryManager = new ReceiveMessageFactoryManager(logger);
            _logger = logger;
            _pipe = new Pipe();
        }

        public MeadowSerialDataProcessor(SerialPort serialPort, ILogger? logger = null) : this(logger ?? new NullLogger<MeadowSerialDataProcessor>())
        {
            _serialPort = serialPort;
            //_pipeWriterTask = Task.Factory.StartNew(async () => await ReadSerialPortData(_pipe.Writer), TaskCreationOptions.LongRunning);
            //_pipeReaderTask = Task.Factory.StartNew(async () => await ProcessPipeData(_pipe.Reader), TaskCreationOptions.LongRunning);
            _dataProcessorTask = Task.Factory.StartNew(ReadSerialPortAsync, TaskCreationOptions.LongRunning);
        }

        public MeadowSerialDataProcessor(Socket socket, ILogger? logger = null) : this(logger ?? new NullLogger<MeadowSerialDataProcessor>())
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

                    await DecodeAndProcessPacket(buffer[..receivedLength], _cts.Token).ConfigureAwait(false);

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
            private readonly IList<byte[]> _segments;

            public SerialMessage(byte[] segment)
            {
                _segments = new List<byte[]>();
                _segments.Add(segment);
            }

            public void AddSegment(byte[] segment)
            {
                _segments.Add(segment);
            }

            public byte[] ToArray()
            {
                var offset = 0;
                var arr = new byte[_segments.Sum(x => x.Length)];
                foreach (var segment in _segments)
                {
                    segment.CopyTo(arr, offset);
                    offset += segment.Length;
                }
                return arr;
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
                        if (!_serialPort.IsOpen)
                        {
                            try
                            {
                                _serialPort.Open();
                                continue;
                            }
                            catch (Exception)
                            {
                                await Task.Delay(1000)
                                          .ConfigureAwait(false);

                                continue;
                            }
                        }

                        var buffer = new byte[1024];
                        var receivedLength = await _serialPort.BaseStream.ReadAsync(buffer)
                                                              .ConfigureAwait(false);
                        buffer = buffer[..receivedLength];
                        while (buffer.Length > 0)
                        {
                            var messageEnd = Array.IndexOf(buffer, (byte)0x00);
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
                                    var msg = buffer[..messageEnd];
                                    buffer = buffer[(messageEnd+1)..];
                                    await DecodeAndProcessPacket(msg, _cts.Token)
                                        .ConfigureAwait(false);
                                }
                                // We had some part of the message from a previous iteration
                                else
                                {
                                    message.AddSegment(buffer[..messageEnd]);
                                    buffer = buffer[(messageEnd+1)..];
                                    var msg = message.ToArray();
                                    await DecodeAndProcessPacket(msg, _cts.Token)
                                        .ConfigureAwait(false);

                                    message = null;
                                }
                            }
                        }
                    }
                    catch(Exception ex)
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

        //private async Task ReadSerialPortData(PipeWriter pipeWriter)
        //{
        //    const int minimumBufferSize = 1024;
        //    while (!_cts.IsCancellationRequested)
        //    {
        //        // Allocate at least 512 bytes from the PipeWriter
        //        var memory = pipeWriter.GetMemory(minimumBufferSize);
        //        try 
        //        {
        //            var bytesRead = await _serialPort.BaseStream.ReadAsync(memory, _cts.Token).ConfigureAwait(false);
        //            _logger.LogTrace("Read {count} bytes from the serial port", bytesRead);
        //            // Tell the PipeWriter how much was read from the Socket
        //            pipeWriter.Advance(bytesRead);
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogDebug(ex, "Error reading from stream");
        //            if (!_serialPort.IsOpen)
        //            {
        //                try
        //                {
        //                    _serialPort.Open();
        //                    continue;
        //                }
        //                catch (Exception)
        //                {
        //                    await Task.Delay(1000)
        //                              .ConfigureAwait(false);
        //                    continue;
        //                }
        //            }
        //            break;
        //        }

        //        // Make the data available to the PipeReader
        //        var result = await pipeWriter.FlushAsync(_cts.Token).ConfigureAwait(false);

        //        if (result.IsCompleted)
        //        {
        //            break;
        //        }
        //    }
        //}

        //private async Task ProcessPipeData(PipeReader pipeReader)
        //{
        //    while (true)
        //    {
        //        try
        //        {
        //            var result = await pipeReader.ReadAsync(_cts.Token)
        //                                         .ConfigureAwait(false);

        //            var buffer = result.Buffer;
        //            SequencePosition? position;

        //            do
        //            {
        //                // Look for a EOL in the buffer
        //                position = buffer.PositionOf((byte)0);

        //                if (position != null)
        //                {
        //                    var sequence = buffer.Slice(0, position.Value);
        //                    if (sequence.Length > 0)
        //                    {
        //                        var sequenceArray = sequence.ToArray();
        //                        if (sequenceArray[^1] != 0x00)
        //                        {
        //                            throw new Exception("sadness");
        //                        }
        //                        await DecodeAndProcessPacket(sequence, _cts.Token);
        //                    }

        //                    buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
        //                }
        //            } while (position != null);

        //            // Tell the PipeReader how much of the buffer we have consumed
        //            pipeReader.AdvanceTo(buffer.Start, buffer.End);

        //            // Stop reading if there's no more data coming
        //            if (result.IsCompleted)
        //            {
        //                break;
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            _logger.LogError(ex, "");
        //            throw;
        //        }
        //    }

        //    // Mark the PipeReader as complete
        //    await pipeReader.CompleteAsync().ConfigureAwait(false);
        //}

        //private async Task<bool> DecodeAndProcessPacket(ReadOnlySequence<byte> packetBuffer, CancellationToken cancellationToken)
        //{
        //    var decodedBuffer = ArrayPool<byte>.Shared.Rent(MeadowDeviceManager.MaxAllowableMsgPacketLength);
        //    var packetLength = packetBuffer.Length;
        //    // It's possible that we may find a series of 0x00 values in the buffer.
        //    // This is because when the sender is blocked (because this code isn't
        //    // running) it will attempt to send a single 0x00 before the full message.
        //    // This allows it to test for a connection. When the connection is
        //    // unblocked this 0x00 is sent and gets put into the buffer along with
        //    // any others that were queued along the usb serial pipe line.
        //    if (packetLength == 1)
        //    {
        //        //_logger.LogTrace("Throwing out 0x00 from buffer");
        //        return false;
        //    }

        //    var decodedSize = CobsTools.CobsDecoding(packetBuffer, ref decodedBuffer);

        //    // If a message is too short it is ignored
        //    if (decodedSize < MeadowDeviceManager.ProtocolHeaderSize)
        //        return false;

        //    _logger.LogTrace("Decoded {count} into {count}", packetBuffer.Length, decodedSize);

        //    Debug.Assert(decodedSize <= MeadowDeviceManager.MaxAllowableMsgPacketLength);

        //    // Process the received packet
        //    await ParseAndProcessReceivedPacket(
        //            decodedBuffer[..decodedSize],
        //            cancellationToken)
        //        .ConfigureAwait(false);

        //    return true;
        //}

        private async Task<bool> DecodeAndProcessPacket(Memory<byte> packetBuffer, CancellationToken cancellationToken)
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
            await ParseAndProcessReceivedPacket(
                           decodedBuffer[..decodedSize],
                           cancellationToken)
                       .ConfigureAwait(false);

            return true;
        }

        private async Task ParseAndProcessReceivedPacket(byte[] receivedMsg,
                                                         CancellationToken cancellationToken)
        {
            try
            {
                var processor = _receiveMessageFactoryManager.CreateProcessor(receivedMsg);
                if (processor == null) return;

                if (processor.Execute(receivedMsg))
                {
                    var requestType = (HcomHostRequestType)processor.RequestType;
                    _logger.LogTrace("Received message {messageType}, Content: {messageContent}", requestType, processor.ToString());
                    switch (requestType)
                    {
                        case HcomHostRequestType.HCOM_HOST_REQUEST_UNDEFINED_REQUEST:
                            break;

                        // This set are responses to request issued by this application
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_REJECTED:
                            if (!string.IsNullOrEmpty(processor.ToString()))
                            {
                                OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.Data, processor.ToString()));
                            }
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_ACCEPTED:
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.Accepted));
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_CONCLUDED:
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.Concluded));
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_ERROR:
                            if (!string.IsNullOrEmpty(processor.ToString()))
                            {
                                OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.Data, processor.ToString()));
                            }
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_INFORMATION:
                        {
                            var msg = processor.ToString();
                            if (!string.IsNullOrEmpty(processor.ToString()))
                                OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.Data, msg));

                            break;
                        }
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_LIST_HEADER:
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.FileListTitle, processor.ToString()));
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_LIST_MEMBER:
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.FileListMember, processor.ToString()));
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_CRC_MEMBER:
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.FileListCrcMember, processor.ToString()));
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_MONO_STDOUT:
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.AppOutput, processor.ToString()));
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_MONO_STDERR:
                            _logger.LogWarning(processor.ToString());
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.ErrOutput, processor.ToString()));
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_DEVICE_INFO:
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.DeviceInfo, processor.ToString()));
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_TRACE_MSG:
                            if (!string.IsNullOrEmpty(processor.ToString()))
                            {
                                OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.MeadowTrace, processor.ToString()));
                            }
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_RECONNECT:
                            await Task.Delay(2000, cancellationToken).ConfigureAwait(false); // need to give the device a couple seconds
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.SerialReconnect, null));
                            break;

                        // Debug message from Meadow for Visual Studio
                        case HcomHostRequestType.HCOM_HOST_REQUEST_DEBUGGING_MONO_DATA:
                            if (ForwardDebuggingData != null)
                                await ForwardDebuggingData(processor.MessageData, cancellationToken)
                                    .ConfigureAwait(false);
                            break;
                        case HcomHostRequestType.HCOM_HOST_REQUEST_FILE_START_OKAY:
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.DownloadStartOkay));
                            break;

                        case HcomHostRequestType.HCOM_HOST_REQUEST_FILE_START_FAIL:
                            OnReceiveData?.Invoke(this, new MeadowMessageEventArgs(MeadowMessageType.DownloadStartFail));
                            break;

                        case HcomHostRequestType.HCOM_HOST_REQUEST_GET_INITIAL_FILE_BYTES:
                        {
                            // Just length and hex-hex-hex....
                            // Console.WriteLine($"Received {processor.MessageData.Length} bytes. They look like this: {Environment.NewLine}{BitConverter.ToString(processor.MessageData)}");

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
        public void Dispose()
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