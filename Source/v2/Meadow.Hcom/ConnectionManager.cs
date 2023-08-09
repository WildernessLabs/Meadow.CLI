namespace Meadow.Hcom
{
    public class ConnectionManager
    {
        private static List<IMeadowConnection> _connections = new List<IMeadowConnection>();

        public static TConnection GetConnection<TConnection>(string connectionName)
            where TConnection : class, IMeadowConnection
        {
            // see if it already is known
            var existing = _connections.FirstOrDefault(c => c.Name == connectionName) as TConnection;
            if (existing != null) return existing;

            // otherwise create
            switch (typeof(TConnection))
            {
                case Type t when t == typeof(SerialConnection):
                    var c = new SerialConnection(connectionName);
                    _connections.Add(c);
#pragma warning disable 8603
                    return c as TConnection;
#pragma warning restore
                default:
                    throw new NotSupportedException();
            };

        }
    }
}