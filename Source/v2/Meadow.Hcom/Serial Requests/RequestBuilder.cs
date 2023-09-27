namespace Meadow.Hcom
{
    public static class RequestBuilder
    {
        private static uint _sequenceNumber;

        public static T Build<T>(uint userData = 0, ushort extraData = 0, ushort protocol = Protocol.HCOM_PROTOCOL_HCOM_VERSION_NUMBER)
            where T : Request, new()
        {
            return new T
            {
                SequenceNumber = 0,
                ProtocolVersion = protocol,
                UserData = userData,
                ExtraData = extraData
            };
        }
    }
}