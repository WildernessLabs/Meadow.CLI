using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MeadowCLI.DeviceManagement;
using static MeadowCLI.DeviceManagement.MeadowFileManager;

namespace Meadow.CLI.Internals.MeadowComms.RecvClasses
{

    public enum RequestTypeFromMeadow
    {
        SimpleMsg = 0x01 | MeadowFileManager.HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE,
        SimpleText = 0x01 | MeadowFileManager.HcomProtocolHeaderTypes.HCOM_PROTOCOL_HEADER_TYPE_SIMPLE_TEXT,
    }

    public abstract class RecvMessageFactory
    {
        public abstract IReceivedMessage Create(byte[] recvdMsg);
    }

    public class RecvFactoryManager
    {
        private readonly Dictionary<RequestTypeFromMeadow, RecvMessageFactory> _factories;
    
        public RecvFactoryManager()
        {
            // A factory for each unique request type
            _factories = new Dictionary<RequestTypeFromMeadow, RecvMessageFactory>
            {
                {RequestTypeFromMeadow.SimpleMsg, new RecvSimpleMsgFactory() },
                {RequestTypeFromMeadow.SimpleText, new RecvSimpleTextFactory() },
            };
        }

        public IReceivedMessage CreateProcessor(byte[] recvdMsg)
        {
            try
            {
                RequestTypeFromMeadow rqstType = FindRequestTypeValue(recvdMsg);
                RecvMessageFactory factory = _factories[rqstType];
                return factory.Create(recvdMsg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                throw;
            }
        }

        RequestTypeFromMeadow FindRequestTypeValue(byte[] recvdMsg)
        {
            int RequestTypeOffset = (int)HcomProtocolHeaderOffsets.HCOM_PROTOCOL_REQUEST_HEADER_RQST_TYPE_OFFSET;
            return (RequestTypeFromMeadow) Convert.ToUInt16(recvdMsg[RequestTypeOffset] + (recvdMsg[RequestTypeOffset + 1] << 8));
        }
    }
}
