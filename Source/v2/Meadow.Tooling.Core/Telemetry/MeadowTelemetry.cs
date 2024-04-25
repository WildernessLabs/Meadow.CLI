using Meadow.CLI;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

namespace Meadow.Telemetry;

public sealed class MeadowTelemetry : IDisposable
{
    private const string ConnectionString = "InstrumentationKey=1c5d5d93-e50e-47bc-b14e-23e32dc1bcac";
    private static readonly Lazy<MeadowTelemetry> _current = new(() => new MeadowTelemetry(new SettingsManager()));

    public const string TelemetryEnvironmentVariable = "MEADOW_TELEMETRY";
    public const string MachineIdSettingName = "private.telemetry.machineid";
    public const string TelemetryEnabledSettingName = "telemetry.enabled";

    private readonly ISettingsManager _settingsManager;
    private TelemetryConfiguration? _telemetryConfiguration;

    public TelemetryClient? TelemetryClient { get; private set; }

    public static MeadowTelemetry Current => _current.Value;

    public MeadowTelemetry(ISettingsManager settingsManager)
    {
        _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
        Initialize();
    }

    private void Initialize()
    {
        if (TelemetryClient != null)
        {
            return;
        }

        _telemetryConfiguration = CreateTelemetryConfiguration();

        if (_telemetryConfiguration == null)
        {
            return;
        }

        TelemetryClient = new TelemetryClient(_telemetryConfiguration);
        ConfigureTelemetryContext(TelemetryClient.Context);
    }

    public void Dispose()
    {
        TelemetryClient?.FlushAsync(CancellationToken.None).ConfigureAwait(false).GetAwaiter().GetResult();
        _telemetryConfiguration?.Dispose();

        TelemetryClient = null;
        _telemetryConfiguration = null;
    }

    public bool IsEnabled
    {
        get
        {
            if (CIEnvironmentDetector.IsCIEnvironment())
            {
                return false;
            }

            if (bool.TryParse(Environment.GetEnvironmentVariable(TelemetryEnvironmentVariable), out bool isEnabled))
            {
                return isEnabled;
            }

            if (bool.TryParse(_settingsManager.GetSetting(TelemetryEnabledSettingName), out isEnabled))
            {
                return isEnabled;
            }

            return false;
        }
    }

    public bool ShouldAskForConsent
    {
        get
        {
            if (CIEnvironmentDetector.IsCIEnvironment())
            {
                return false;
            }

            var envVar = Environment.GetEnvironmentVariable(TelemetryEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(envVar) && bool.TryParse(envVar, out _))
            {
                return false;
            }

            var setting = _settingsManager.GetSetting(TelemetryEnabledSettingName);
            if (!string.IsNullOrWhiteSpace(setting) && bool.TryParse(setting, out _))
            {
                return false;
            }

            return true;
        }
    }

    public void SetTelemetryEnabled(bool enabled)
    {
        _settingsManager.SaveSetting(TelemetryEnabledSettingName, enabled.ToString().ToLowerInvariant());

        if (enabled)
        {
            Initialize();
        }
        else
        {
            Dispose();
            _settingsManager.DeleteSetting(MachineIdSettingName);
        }
    }

    public void TrackCommand(string? commandName)
    {
        if (TelemetryClient == null)
        {
            return;
        }

        var eventTelemetry = new EventTelemetry("meadow/cli/command")
        {
            Timestamp = DateTimeOffset.UtcNow
        };

        eventTelemetry.Properties["Command"] = commandName?.ToLowerInvariant() ?? "<unknown>";
        TelemetryClient.TrackEvent(eventTelemetry);
    }

    private TelemetryConfiguration? CreateTelemetryConfiguration()
    {
        if (!IsEnabled)
        {
            return null;
        }

        var telemetryConfiguration = new TelemetryConfiguration
        {
            ConnectionString = ConnectionString
        };

#if DEBUG
        telemetryConfiguration.TelemetryChannel.DeveloperMode = true;
#endif

        return telemetryConfiguration;
    }

    private void ConfigureTelemetryContext(TelemetryContext context)
    {
        context.Cloud.RoleInstance = "<undefined>";
        context.Device.Type = "<undefined>";
        context.Location.Ip = "0.0.0.0";
        context.Session.Id = GetRandomString(24);
        context.User.Id = GetMachineId();

        context.GlobalProperties["OS Version"] = RuntimeEnvironment.OperatingSystemVersion;
        context.GlobalProperties["OS Platform"] = RuntimeEnvironment.OperatingSystemPlatform.ToString();
        context.GlobalProperties["OS Architecture"] = RuntimeInformation.OSArchitecture.ToString();
        context.GlobalProperties["Kernel Version"] = RuntimeInformation.OSDescription;
        context.GlobalProperties["Runtime Identifier"] = AppContext.GetData("RUNTIME_IDENTIFIER") as string ?? "<unknown>";
        context.GlobalProperties["Assembly Name"] = Assembly.GetEntryAssembly()?.GetName().Name;
        context.GlobalProperties["Assembly Version"] = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3);
    }

    private string GetMachineId()
    {
        var machineId = _settingsManager.GetSetting(MachineIdSettingName) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(machineId))
        {
            machineId = Guid.NewGuid().ToString("N");
            _settingsManager.SaveSetting(MachineIdSettingName, machineId);
        }

        return machineId;
    }

    private static string GetRandomString(int length)
    {
        using var rng = RandomNumberGenerator.Create();
        byte[] bytes = new byte[length];
        rng.GetBytes(bytes);

        return Convert.ToBase64String(bytes);
    }
}
