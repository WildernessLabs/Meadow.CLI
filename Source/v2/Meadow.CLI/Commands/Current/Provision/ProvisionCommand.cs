using System.Collections.Concurrent;
using System.Threading.Tasks;
using CliFx.Attributes;
using Meadow.CLI.Commands.DeviceManagement;
using Meadow.Cloud.Client;
using Meadow.LibUsb;
using Meadow.Package;
using Meadow.Software;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Spectre.Console;

namespace Meadow.CLI.Commands.Provision;

[Command("provision", Description = Strings.Provision.CommandDescription)]
public class ProvisionCommand : BaseDeviceCommand<ProvisionCommand>
{
    public const string DefaultOSVersion = "1.12.0.0";
    private string? appPath;
    private string? configuration = "Release";

    [CommandOption("version", 'v', Description = Strings.Provision.CommandOptionVersion, IsRequired = false)]
    public string? OsVersion { get; set; } = DefaultOSVersion;

    [CommandOption("path", 'p', Description = Strings.Provision.CommandOptionPath, IsRequired = false)]
    public string? Path { get; set; } = ".";

    private ConcurrentQueue<ILibUsbDevice> bootloaderDeviceQueue = new ConcurrentQueue<ILibUsbDevice>();

    private List<string> selectedDeviceList = default!;
    private ISettingsManager settingsManager;
    private FileManager fileManager;
    private IMeadowCloudClient meadowCloudClient;
    private MeadowConnectionManager connectionManager;
    private IPackageManager packageManager;
    private bool deployApp = true;

    public ProvisionCommand(ISettingsManager settingsManager, FileManager fileManager,
        IMeadowCloudClient meadowCloudClient, IPackageManager packageManager, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        this.settingsManager = settingsManager;
        this.fileManager = fileManager;
        this.meadowCloudClient = meadowCloudClient;
        this.connectionManager = connectionManager;
        this.packageManager = packageManager;
    }

    protected override async ValueTask ExecuteCommand()
    {
        try
        {
            AnsiConsole.MarkupLine(Strings.Provision.RunningTitle);

            bool refreshDeviceList = false;
            do
            {
                UpdateDeviceList(CancellationToken);

                if (bootloaderDeviceQueue.Count == 0)
                {
                    Logger?.LogError(Strings.Provision.NoDevicesFound);
                    return;
                }

                var multiSelectionPrompt = new MultiSelectionPrompt<string>()
                    .Title(Strings.Provision.PromptTitle)
                    .PageSize(15)
                    .NotRequired() // Can be Blank to exit
                    .MoreChoicesText($"[grey]{Strings.Provision.MoreChoicesInstructions}[/]")
                    .InstructionsText(string.Format($"[grey]{Strings.Provision.Instructions}[/]", $"[blue]<{Strings.Space}>[/]", $"[green]<{Strings.Enter}>[/]"))
                    .UseConverter(x => x);

                foreach (var device in bootloaderDeviceQueue)
                {
                    multiSelectionPrompt.AddChoices(device.SerialNumber);
                }

                selectedDeviceList = AnsiConsole.Prompt(multiSelectionPrompt);

                if (selectedDeviceList.Count == 0)
                {
                    AnsiConsole.MarkupLine($"[yellow]{Strings.Provision.NoDeviceSelected}[/]");
                    return;
                }

                var selectedDeviceTable = new Table();
                selectedDeviceTable.AddColumn(Strings.Provision.ColumnTitle);

                foreach (var device in selectedDeviceList)
                {
                    selectedDeviceTable.AddRow(device);
                }

                AnsiConsole.Write(selectedDeviceTable);

                refreshDeviceList = AnsiConsole.Confirm(Strings.Provision.RefreshDeviceList);
            } while (!refreshDeviceList);

            string path = AppTools.ValidateAndSanitizeAppPath(System.IO.Path.Combine(Path, "provision.json"));

            if (!string.IsNullOrWhiteSpace(path)
                && !File.Exists(path))
            {
                deployApp = false;
                AnsiConsole.MarkupLine($"[red]{Strings.Provision.FileNotFound}[/]", $"[yellow]{path}[/]");
                AnsiConsole.MarkupLine(Strings.Provision.NoAppDeployment, $"[yellow]{OsVersion}[/]");
            }
            else
            {
                Path = path;
            }

            if (deployApp)
            {
                var provisionSettings = JsonConvert.DeserializeObject<ProvisionSettings>(File.ReadAllText(Path!));
                if (provisionSettings == null)
                {
                    throw new Exception("Failed to read provision.json file.");
                }

                // Use the settings from provisionSettings as needed
                try
                {
                    appPath = AppTools.ValidateAndSanitizeAppPath(provisionSettings.AppPath);

                    if (!File.Exists(appPath))
                    {
                        throw new FileNotFoundException($"App.dll Not found at location:{appPath}");
                    }

                    configuration = provisionSettings.Configuration;
                    OsVersion = provisionSettings.OsVersion;

                    AnsiConsole.MarkupLine(Strings.Provision.TrimmingApp);
                    await AppTools.TrimApplication(appPath!, packageManager, OsVersion!, configuration, null, null, Console, CancellationToken);
                }
                catch (Exception ex)
                {
                    // Eat the exception and keep going.
                    deployApp = false;
                    // TODO put this back in for release #if DEBUG
                    var message = ex.Message;
                    var stackTrace = ex.StackTrace;
                    message += Environment.NewLine + stackTrace;
                    AnsiConsole.MarkupLine($"[red]{ex.Message}[/]");
                    //#endif
                    AnsiConsole.MarkupLine(Strings.Provision.NoAppDeployment, $"[yellow]{OsVersion}[/]");
                }
            }

            if (string.IsNullOrEmpty(OsVersion))
            {
                OsVersion = DefaultOSVersion;
            }

            // Install DFU, if it's not already installed.
            var dfuInstallCommand = new DfuInstallCommand(settingsManager, LoggerFactory);
            await dfuInstallCommand.ExecuteAsync(Console);

            // Make sure we've downloaded the osVersion or default
            var firmwareDownloadCommand = new FirmwareDownloadCommand(fileManager, meadowCloudClient, LoggerFactory)
            {
                Version = OsVersion,
                Force = true
            };
            await firmwareDownloadCommand.ExecuteAsync(Console);


            // If we've reached here we're ready to Flash
            await FlashingAttachedDevices();
        }
        catch (Exception ex)
        {

            var message = ex.Message;
#if DEBUG
            var stackTrace = ex.StackTrace;
            message += Environment.NewLine + stackTrace;
#endif
            AnsiConsole.MarkupLine($"[red]{message}[/]");
        }
    }

    private void UpdateDeviceList(CancellationToken cancellationToken)
    {
        var ourDevices = GetValidUsbDevices();

        if (ourDevices?.Count() > 0)
        {
            bootloaderDeviceQueue.Clear();

            foreach (ILibUsbDevice device in ourDevices)
            {
                if (bootloaderDeviceQueue != null)
                {
                    if (device != null)
                    {
                        bootloaderDeviceQueue.Enqueue(device);
                    }
                }
            }
        }
    }

    private IEnumerable<ILibUsbDevice>? GetValidUsbDevices()
    {
        try
        {
            var provider = new LibUsbProvider();

            var devices = provider.GetDevicesInBootloaderMode();

            return devices;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task FlashingAttachedDevices()
    {
        var succeedCount = 0;
        await AnsiConsole.Progress()
            .AutoRefresh(true)
            .HideCompleted(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),    // Task description
                new ProgressBarColumn(),        // Progress bar
                new PercentageColumn(),         // Percentage
                new SpinnerColumn(),            // Spinner
            })
            .StartAsync(async ctx =>
            {
                foreach (var deviceSerialNumber in selectedDeviceList)
                {
                    var formatedDevice = $"[green]{deviceSerialNumber}[/]";
                    var task = ctx.AddTask(formatedDevice, maxValue: 100);

                    try
                    {
                        var firmareUpdater = new FirmwareUpdater<ProvisionCommand>(this, settingsManager, fileManager, this.connectionManager, null, null, true, OsVersion, deviceSerialNumber, null, CancellationToken);
                        firmareUpdater.UpdateProgress += (o, e) =>
                        {
                            if (e.percentage > 0)
                            {
                                task.Value = e.percentage;
                            }
                            task.Description = $"{formatedDevice}: {e.message}";
                        };

                        if (!await firmareUpdater.UpdateFirmware())
                        {
                            task.Description = $"{formatedDevice}: [red]{Strings.Provision.UpdateFailed}[/]";
                            task.StopTask();
                        }

                        if (deployApp)
                        {
                            task.Increment(20.00);
                            task.Description = $"{Strings.Provision.DeployingApp}";

                            var route = await MeadowConnectionManager.GetRouteFromSerialNumber(deviceSerialNumber!);
                            if (!string.IsNullOrWhiteSpace(route))
                            {
                                var connection = await GetConnectionForRoute(route, true);
                                var appDir = System.IO.Path.GetDirectoryName(appPath);
                                await AppManager.DeployApplication(packageManager, connection, OsVersion!, appDir!, true, false, null, CancellationToken);

                                await connection?.Device?.RuntimeEnable(CancellationToken);
                            }
                        }

                        task.Value = 100.00;
                        task.Description = string.Format($"{formatedDevice}: [green]{Strings.Provision.UpdateComplete}[/]");

                        task.StopTask();

                        await Task.Delay(2000);

                        succeedCount++;
                    }
                    catch (Exception ex)
                    {
                        task.Description = $"{formatedDevice}: [red]{ex.Message}[/]";
                        task.StopTask();
#if DEBUG
                        AnsiConsole.MarkupLine($"[red]{ex.Message}[/]{Environment.NewLine}{ex.StackTrace}");
#endif
                    }
                }
            });

        if (succeedCount == selectedDeviceList.Count)
        {
            AnsiConsole.MarkupLine($"[green]{Strings.Provision.AllDevicesFlashed}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]There were issues. Please check previous error messages.[/]");
        }
    }
}

public class ProvisionSettings
{
    public string? AppPath { get; set; }
    public string? Configuration { get; set; }
    public string? OsVersion { get; set; }
}