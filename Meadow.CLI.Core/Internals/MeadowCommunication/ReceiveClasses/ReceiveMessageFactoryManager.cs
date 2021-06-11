using System;
using System.Collections.Generic;

using Meadow.CLI.Core.DeviceManagement;

using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Core.Internals.MeadowCommunication.ReceiveClasses
{
    public class ReceiveMessageFactoryManager
    {
        private readonly Dictionary<HcomHostRequestType, ReceiveMessageFactory> _factories;
        private readonly ILogger _logger;
        public ReceiveMessageFactoryManager(ILogger logger)
        {
            _logger = logger;
            _factories = new Dictionary<HcomHostRequestType, ReceiveMessageFactory>
            {
                {HcomHostRequestType.HCOM_HOST_REQUEST_DEBUGGING_MONO_DATA, new ReceiveSimpleBinaryFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_GET_INITIAL_FILE_BYTES, new ReceiveSimpleBinaryFactory() },

                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_REJECTED, new ReceiveSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_ACCEPTED, new ReceiveSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_CONCLUDED, new ReceiveSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_ERROR, new ReceiveSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_INFORMATION, new ReceiveSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_LIST_HEADER, new ReceiveSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_LIST_MEMBER, new ReceiveSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_CRC_MEMBER, new ReceiveSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_MONO_STDOUT, new ReceiveSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_DEVICE_INFO, new ReceiveSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_TRACE_MSG, new ReceiveSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_RECONNECT, new ReceiveSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_MONO_STDERR, new ReceiveSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_FILE_START_OKAY, new ReceiveSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_FILE_START_FAIL, new ReceiveSimpleTextFactory() },
            };
        }

        public IReceivedMessage? CreateProcessor(byte[] receivedMessage)
        {
            var requestType = HcomHostRequestType.HCOM_HOST_REQUEST_UNDEFINED_REQUEST;
            try
            {
                requestType = FindRequestTypeValue(receivedMessage);
                ReceiveMessageFactory factory = _factories[requestType];
                return factory.Create(receivedMessage);
            }
            catch (KeyNotFoundException)
            {
                _logger.LogWarning($"An unknown request value of '0x{requestType:x}' was received.");
                return null;
            }
            catch (Exception ex)
            {
                // I saw a few time, that this exception was being thrown. It was caused by
                // corrupted data being processed.
                _logger.LogWarning(ex, $"Request type was: 0x{requestType:x}");
                return null;
            }
        }

        private static HcomHostRequestType FindRequestTypeValue(IReadOnlyList<byte> receivedMessage)
        {
            const int requestTypeOffset = (int)HcomProtocolHeaderOffsets.HCOM_PROTOCOL_REQUEST_HEADER_RQST_TYPE_OFFSET;
            return (HcomHostRequestType)Convert.ToUInt16(receivedMessage[requestTypeOffset] + (receivedMessage[requestTypeOffset + 1] << 8));
        }
    }
}
