using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
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

    private ConcurrentQueue<ILibUsbDevice> bootloaderDeviceQueue = new ConcurrentQueue<ILibUsbDevice>();

    private List<string> selectedDeviceList = default!;
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

            var multiSelectionPrompt = new MultiSelectionPrompt<string>()
                .Title(Strings.Provision.PromptTitle)
                .PageSize(15)
                .NotRequired() // Can be Blank to exit
                .MoreChoicesText($"[grey]{Strings.Provision.MoreChoicesInstructions}[/]")
                .InstructionsText(string.Format($"[grey]{Strings.Provision.Instructions}[/]", $"[blue]<{Strings.Space}>[/]", $"[green]<{Strings.Enter}>[/]"))
                .UseConverter(x => x);

            foreach (var device in bootloaderDeviceQueue)
            {
                multiSelectionPrompt.AddChoices(device.GetDeviceSerialNumber());
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
                foreach (var deviceSerialNumber in selectedDeviceList)
                {
                    var formatedDevice = $"[green]{deviceSerialNumber}[/]";
                    var task = ctx.AddTask(formatedDevice, maxValue: 100);

                    var firmareUpdater = new FirmwareUpdater<ProvisionCommand>(this, settingsManager, fileManager, this.connectionManager, null, null, true, OsVersion, deviceSerialNumber, null, CancellationToken);
                    firmareUpdater.UpdateProgress += (o, e) =>
                    {
                        task.Increment(20.00);
                        task.Description = string.Format($"{formatedDevice}: {e}");
                    };

                    task.Increment(20.00);
                    if (!await firmareUpdater.UpdateFirmware())
                    {
                        task.Description = string.Format($"{formatedDevice}: [red]{Strings.Provision.UpdateFailed}[/]");
                        task.StopTask();
                    }

                    task.Value = 100.00;
                    task.Description = string.Format($"{formatedDevice}: [green]{Strings.Provision.UpdateComplete}[/]");

                    task.StopTask();

                    await Task.Delay(2000);
                }
            });

        AnsiConsole.MarkupLine($"[green]{Strings.Provision.AllDevicesFlashed}[/]");
    }
}