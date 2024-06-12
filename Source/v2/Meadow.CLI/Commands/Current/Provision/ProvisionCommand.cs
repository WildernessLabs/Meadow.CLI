using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Management;
using CliFx.Attributes;
using Meadow.CLI.Commands.DeviceManagement;
using Meadow.Cloud.Client;
using Meadow.LibUsb;
using Meadow.Software;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using YamlDotNet.Serialization;

namespace Meadow.CLI.Commands.Provision;

[Command("provision", Description = Strings.Provision.CommandDescription)]
public class ProvisionCommand : BaseDeviceCommand<ProvisionCommand>
{
    public const string DefaultOSVersion = "1.12.0.0";
    [CommandOption("version", 'v', Description = Strings.Provision.CommandOptionVersion, IsRequired = false)]
    public string? OsVersion { get; set; } = DefaultOSVersion;

    private ConcurrentQueue<BootLoaderDevice> bootloaderDeviceQueue = new ConcurrentQueue<BootLoaderDevice>();

    private List<BootLoaderDevice> selectedDevices = default!;
    private ISettingsManager settingsManager;
    private FileManager fileManager;
    private IMeadowCloudClient meadowCloudClient;
    private MeadowConnectionManager connectionManager;

    public ProvisionCommand(ISettingsManager settingsManager, FileManager fileManager,
        IMeadowCloudClient meadowCloudClient, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(connectionManager, loggerFactory)
    {
        this.settingsManager = settingsManager;
        this.fileManager = fileManager;
        this.meadowCloudClient = meadowCloudClient;
        this.connectionManager = connectionManager;
    }

    protected override async ValueTask ExecuteCommand()
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

            var multiSelectionPrompt = new MultiSelectionPrompt<BootLoaderDevice>()
                .Title(Strings.Provision.PromptTitle)
                .PageSize(15)
                .NotRequired() // Can be Blank to exit
                .MoreChoicesText($"[grey]{Strings.Provision.MoreChoicesInstructions}[/]")
                .InstructionsText(string.Format($"[grey]{Strings.Provision.Instructions}[/]", $"[blue]<{Strings.Space}>[/]", $"[green]<{Strings.Enter}>[/]"))
                .UseConverter(x => x.SerialPort);

            foreach (var device in bootloaderDeviceQueue)
            {
                multiSelectionPrompt.AddChoices(device);
            }

            selectedDevices = AnsiConsole.Prompt(multiSelectionPrompt);

            var selectedDeviceTable = new Table();
            selectedDeviceTable.AddColumn(Strings.Provision.ColumnTitle);

            foreach (var device in selectedDevices)
            {
                selectedDeviceTable.AddRow(device.SerialPort);
            }

            AnsiConsole.Write(selectedDeviceTable);

            refreshDeviceList = AnsiConsole.Confirm(Strings.Provision.RefreshDeviceList);
        } while (!refreshDeviceList);


        if (selectedDevices.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]{Strings.Provision.NoDeviceSelected}[/]");
            return;
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
                        var deviceSerialNumber = device.GetDeviceSerialNumber();

                        var serialPort = (Environment.OSVersion.Platform == PlatformID.Unix
                            || Environment.OSVersion.Platform == PlatformID.MacOSX)
                            ? $"/dev/tty.usbmodem{deviceSerialNumber}1"
                            : $"COM{GetComPortNumber()}";

                        // Does the serial number match
                        var serialNumberMatch = (bootloaderDeviceQueue.Count(d => d.SerialNumber == deviceSerialNumber) > 0);
                        if (serialNumberMatch || string.IsNullOrEmpty(deviceSerialNumber))
                        {
                            continue;
                        }

                        bootloaderDeviceQueue.Enqueue(new BootLoaderDevice
                        {
                            DeviceObject = device,
                            SerialNumber = deviceSerialNumber,
                            SerialPort = serialPort,
                        });
                    }
                }
            }
        }
    }

    private int GetComPortNumber()
    {

        using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"SELECT * FROM Win32_PnPEntity WHERE DeviceID LIKE 'USB\\VID_" /* TODO + deviceInfo.VendorId.ToString("X4")*/ + "&PID_" /*+ deviceInfo.ProductId.ToString("X4")*/ + "%'"))
        {
            foreach (ManagementObject mo in searcher.Get())
            {
                string deviceId = (string)mo["DeviceID"];
                string portName = deviceId.Substring(deviceId.LastIndexOf("_") + 1);
                return int.Parse(portName.Replace("#", ""));
            }
        }
        throw new Exception("COM port not found");
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
            //footerMessage = ex.Message;
            return null;
        }
    }

    public async Task FlashingAttachedDevices()
    {
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
                var tasklist = new List<Task>();

                foreach (var device in selectedDevices)
                {
                    var formatedDevice = $"[green]{device.SerialPort}[/]";
                    var task = ctx.AddTask(formatedDevice, maxValue: 100);
                    tasklist.Add(Task.Run(async () =>
                    {
                        var firmareUpdater = new FirmwareUpdater<ProvisionCommand>(this, settingsManager, fileManager, this.connectionManager, null, new FirmwareType[] { FirmwareType.OS, FirmwareType.Runtime, FirmwareType.ESP }, true, OsVersion, device.SerialNumber, null, CancellationToken, true);
                        firmareUpdater.UpdateProgress += (o, e) =>
                        {
                            task.Increment(20.00);
                            task.Description = string.Format($"{formatedDevice}: {e}");
                        };

                        if (!await firmareUpdater.UpdateFirmware())
                        {
                            task.Description = string.Format($"{formatedDevice}: [red]{Strings.Provision.UpdateFailed}[/]");
                            task.StopTask();
                        }

                        task.Increment(20.00);
                        task.Description = string.Format($"{formatedDevice}: [green]{Strings.Provision.UpdateComplete}[/]");

                        task.StopTask();
                    }));
                }

                await Task.WhenAll(tasklist);
            });

        AnsiConsole.MarkupLine($"[green]{Strings.Provision.AllDevicesFlashed}[/]");
    }
}

internal class BootLoaderDevice
{
    public ILibUsbDevice? DeviceObject { get; internal set; }
    internal string SerialPort { get; set; } = string.Empty;
    internal string SerialNumber { get; set; } = string.Empty;
    internal string CurrentStatus { get; set; } = string.Empty;
}