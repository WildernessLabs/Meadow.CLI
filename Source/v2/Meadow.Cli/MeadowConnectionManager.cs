using Meadow.Cli;
using Meadow.Hcom;
using System.Net;

namespace Meadow.CLI.Commands.DeviceManagement;

public class MeadowConnectionManager
{
    private ISettingsManager _settingsManager;
    private IMeadowConnection? _currentConnection;

    public MeadowConnectionManager(ISettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
    }

    public IMeadowConnection? GetCurrentConnection()
    {
        var route = _settingsManager.GetSetting(SettingsManager.PublicSettings.Route);

        if (route == null)
        {
            throw new Exception("No 'route' configuration set");
        }

        // TODO: support connection changing (CLI does this rarely as it creates a new connection with each command)
        if (_currentConnection != null) return _currentConnection;

        // try to determine what the route is
        string? uri = null;
        if (route.StartsWith("http"))
        {
            uri = route;
        }
        else if (IPAddress.TryParse(route, out var ipAddress))
        {
            uri = $"http://{route}:5000";
        }
        else if (IPEndPoint.TryParse(route, out var endpoint))
        {
            uri = $"http://{route}";
        }

        if (uri != null)
        {
            _currentConnection = new TcpConnection(uri);
        }
        else
        {
            _currentConnection = new SerialConnection(route);
        }

        return _currentConnection;
    }
}
