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

namespace Meadow.CLI.Commands.Provision;

[Command("provision", Description = Strings.Provision.CommandDescription)]
public class ProvisionCommand : BaseSettingsCommand<ProvisionCommand>
{
    public const string DefaultOSVersion = "1.11.0.0";
    [CommandOption("version", 'v', Description = Strings.Provision.CommandOptionVersion, IsRequired = false)]
    public string? OsVersion { get; set; } = DefaultOSVersion;

    private ConcurrentQueue<BootLoaderDevice> bootloaderDeviceQueue = new ConcurrentQueue<BootLoaderDevice>();

    private List<BootLoaderDevice> selectedDevices = default!;
    private FileManager fileManager;
    private IMeadowCloudClient meadowCloudClient;
    private MeadowConnectionManager connectionManager;

    public ProvisionCommand(ISettingsManager settingsManager, FileManager fileManager,
        IMeadowCloudClient meadowCloudClient, MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(settingsManager, loggerFactory)
    {
        this.fileManager = fileManager;
        this.meadowCloudClient = meadowCloudClient;
        this.connectionManager = connectionManager;
    }

    protected override async ValueTask ExecuteCommand()
    {
        AnsiConsole.MarkupLine(Strings.Provision.RunningTitle);

        if (string.IsNullOrEmpty(OsVersion))
        {
            OsVersion = DefaultOSVersion;
        }

        // Install DFU, if it's not already installed.
        var dfuInstallCommand = new DfuInstallCommand(SettingsManager, LoggerFactory);
        await dfuInstallCommand.ExecuteAsync(Console);

        // Make sure we've downloaded the osVersion or default
        var firmwareDownloadCommand = new FirmwareDownloadCommand(fileManager, meadowCloudClient, LoggerFactory)
        {
            Version = OsVersion,
            Force = true
        };
        await firmwareDownloadCommand.ExecuteAsync(Console);

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
                .MoreChoicesText(Strings.Provision.MoreChoicesInstructions)
                .InstructionsText(Strings.Provision.Instructions)
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
        } while (refreshDeviceList);


        if (selectedDevices.Count == 0)
        {
            AnsiConsole.MarkupLine(Strings.Provision.NoDeviceSelected);
            return;
        }
        else
        {

            await FlashingAttachedDevices();
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

    internal static async Task<int> MeadowCLI(string arg, bool redirectStandardOutput = true, bool redirectStandardError = true, bool redirectStandardInput = false)
    {
        using (var process = new Process())
        {
            // TODO Remove ./ before merging PR, otherwise it won't work
            process.StartInfo.FileName = "./meadow";
            process.StartInfo.Arguments = $"{arg}";
            process.StartInfo.WorkingDirectory = System.AppContext.BaseDirectory;

            process.StartInfo.UseShellExecute = false;

            process.StartInfo.CreateNoWindow = true;

            process.StartInfo.RedirectStandardOutput = redirectStandardOutput;
            process.StartInfo.RedirectStandardError = redirectStandardError;
            process.StartInfo.RedirectStandardInput = redirectStandardInput;

            process.Start();

            await process.WaitForExitAsync();

            return process.ExitCode;
        }
    }

    public async Task FlashingAttachedDevices()
    {
        await AnsiConsole.Progress()
            .AutoRefresh(true)
            .HideCompleted(false)
            .StartAsync(async ctx =>
            {
                var tasklist = new List<Task>();

                foreach (var device in selectedDevices)
                {
                    var task = ctx.AddTask($"[green]{device.SerialPort}[/]", maxValue: 100);
                    tasklist.Add(Task.Run(async () => {
                        
                        var firmwareWrite = new FirmwareWriteCommand(SettingsManager, fileManager, connectionManager, LoggerFactory)
                        {
                            Version = OsVersion,
                            SerialNumber = device.SerialNumber
                        };
                        await firmwareWrite.ExecuteAsync(Console);

                        task.StopTask();
                    }));
                }

                await Task.WhenAll(tasklist);
            });

        AnsiConsole.MarkupLine("[green]All devices flashed![/]");
    }
}

internal class BootLoaderDevice
{
    public ILibUsbDevice? DeviceObject { get; internal set; }
    internal string SerialPort { get; set; } = string.Empty;
    internal string SerialNumber { get; set; } = string.Empty;
    internal string CurrentStatus { get; set; } = string.Empty;
    public async Task Flash(string osVersion, ILogger logger, IProgress<string> progress)
    {
        if (await ProvisionCommand.MeadowCLI($"firmware write -v {osVersion} -s {SerialNumber}") == 0)
        {
        }
        else
        {
            logger?.LogError($"Error flash in {SerialPort} :(");
        }
        progress.Report($"{SerialPort}: Flashing completed successfully.");
    }
}