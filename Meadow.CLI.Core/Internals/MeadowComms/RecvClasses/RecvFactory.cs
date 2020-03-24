﻿using System;
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
          // A factory for each unique request type
            _factories = new Dictionary<HcomHostRequestType, RecvMessageFactory>
            {
                {HcomHostRequestType.HCOM_HOST_REQUEST_MONO_DEBUGGER_MSG, new RecvSimpleBinaryFactory() },

                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_REJECTED, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_ACCEPTED, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_CONCLUDED, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_ERROR, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_INFORMATION, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_LIST_HEADER, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_LIST_MEMBER, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_CRC_MEMBER, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_MONO_MSG, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_DEVICE_INFO, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_TRACE_MSG, new RecvSimpleTextFactory() },
                {HcomHostRequestType.HCOM_HOST_REQUEST_TEXT_RECONNECT, new RecvSimpleTextFactory() },
            };
        }

        public IReceivedMessage CreateProcessor(byte[] recvdMsg, int receivedMsgLen)
        {
            HcomHostRequestType rqstType = HcomHostRequestType.HCOM_HOST_REQUEST_UNDEFINED_REQUEST;
            try
            {
                rqstType = FindRequestTypeValue(recvdMsg);
                RecvMessageFactory factory = _factories[rqstType];
                return factory.Create(recvdMsg, receivedMsgLen);
            }
            catch (Exception ex)
            {
                // I saw a few time, that this exception was being thrown. It was caused by
                // corrupted data being processed.
                Console.WriteLine($"Request type was: {rqstType}. Exception: {ex.Message}");
                System.Threading.Thread.Sleep(1);
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
