using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MeadowCLI.DeviceManagement;
using static MeadowCLI.DeviceManagement.MeadowFileManager;

namespace Meadow.CLI.Internals.MeadowComms.RecvClasses
{
    public abstract class RecvMessageFactory
    {
        public abstract IReceivedMessage Create(byte[] recvdMsg, int recvdMsgLength);
    }

    public class RecvFactoryManager
    {
        private readonly Dictionary<HcomHostRequestType, RecvMessageFactory> _factories;
        public RecvFactoryManager()
        {
          // A factory for each received unique request type
            _factories = new Dictionary<HcomHostRequestType, RecvMessageFactory>
            {
                {HcomHostRequestType.HCOM_HOST_REQUEST_DEBUGGING_MONO_DATA, new RecvSimpleBinaryFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_GET_INITIAL_FILE_BYTES, new RecvSimpleBinaryFactory() },

                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_REJECTED, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_ACCEPTED, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_CONCLUDED, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_ERROR, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_INFORMATION, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_LIST_HEADER, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_LIST_MEMBER, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_CRC_MEMBER, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_MONO_STDOUT, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_DEVICE_INFO, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_TRACE_MSG, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_RECONNECT, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_MONO_STDERR, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_FILE_START_OKAY, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_FILE_START_FAIL, new RecvSimpleTextFactory() },
            };
        }

        public IReceivedMessage CreateProcessor(byte[] recvdMsg, int receivedMsgLen)
        {
            HcomHostRequestType rqstType = HcomHostRequestType.HCOM_HOST_REQUEST_UNDEFINED_REQUEST;
            try
            {
                rqstType = FindRequestTypeValue(recvdMsg);
            Console.WriteLine($"==> {DateTime.Now:HH:mm:ss.fff}-Received message type '{rqstType}'");
                RecvMessageFactory factory = _factories[rqstType];
                return factory.Create(recvdMsg, receivedMsgLen);
            }
            catch (KeyNotFoundException)
            {
                Console.WriteLine($"An unknown request value of '0x{rqstType:x}' was received.");
                return null;
            }
            catch (Exception ex)
            {
                // I saw a few time, that this exception was being thrown. It was caused by
                // corrupted data being processed.
                Console.WriteLine($"Request type was: 0x{rqstType:x}. Exception: {ex.Message}");
                return null;
            }
        }

        HcomHostRequestType FindRequestTypeValue(byte[] recvdMsg)
        {
            int RequestTypeOffset = (int)HcomProtocolHeaderOffsets.HCOM_PROTOCOL_REQUEST_HEADER_RQST_TYPE_OFFSET;
            return (HcomHostRequestType) Convert.ToUInt16(recvdMsg[RequestTypeOffset] + (recvdMsg[RequestTypeOffset + 1] << 8));
        }
    }
}
