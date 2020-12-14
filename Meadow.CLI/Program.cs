using CommandLine;
using System;
using MeadowCLI.DeviceManagement;
using System.IO.Ports;
using System.Threading;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net;
using System.IO.Compression;
using Newtonsoft.Json.Linq;
using System.Linq;
using Meadow.CLI;
using System.Reflection;
using System.Text.Json;

namespace MeadowCLI
{
    class Program
    {
        
        [Flags]
        enum CompletionBehavior
        {
            Success = 0x00,
            RequestFailed = 1 << 0,
            ExitConsole = 1 << 2,
            KeepConsoleOpen = 1 << 3
        }

        static bool _quit = false;

        static void Main(string[] args)
        {
            Console.CancelKeyPress += (s, e) =>
            {
                _quit = true;
                e.Cancel = true;
                MeadowDeviceManager.CurrentDevice?.SerialPort?.Close();
            };

            CompletionBehavior behavior = CompletionBehavior.Success;

            DownloadManager downloadManager = new DownloadManager();
            var check = downloadManager.CheckForUpdates().Result;
            if (check.updateExists)
            {
                Console.WriteLine($"CLI version {check.latestVersion} is available. To update, run: {downloadManager.updateCommand}");
            }

            if (args.Length == 0)
            {
                args = new string[] { "--help" };
            }

            Parser.Default.ParseArguments<Options>(args)
            .WithParsed<Options>(options =>
            {
                if (options.ListPorts)
                {
                    Console.WriteLine("Available serial ports\n----------------------");

                    var ports = MeadowSerialDevice.GetAvailableSerialPorts();
                    if (ports == null || ports.Length == 0)
                    {
                        Console.WriteLine("\t <no ports found>");
                    }
                    else
                    {
                        foreach (var p in ports)
                        {
                            Console.WriteLine($"\t{p}");
                        }
                    }
                    Console.WriteLine($"\n");
                }
                else
                {
                    if (options.DownloadLatest)
                    {
                        var downloadManager = new DownloadManager();
                        downloadManager.DownloadLatest().Wait();
                    }
                    else if (options.FlashOS)
                    {
                        DfuUpload.FlashOS(options.FileName);
                    }
                    else
                    {
                        SyncArgsCache(options);
                        try
                        {
                            behavior = ProcessHcom(options).Result;
                        }
                        catch (Exception)
                        {

                        }
                    }
                    
                }
            });

            //if (System.Diagnostics.Debugger.IsAttached)
            //{
            //    behavior = CompletionBehavior.KeepConsoleOpen;
            //}

            if ((behavior & CompletionBehavior.KeepConsoleOpen) == CompletionBehavior.KeepConsoleOpen)
            {
                Console.Read();
            }
            else
            {
                Thread.Sleep(500);
            }

            Environment.Exit(0);
        }

        static void SyncArgsCache(Options options)
        {
            var port = SettingsManager.GetSetting(Setting.PORT);
            if (string.IsNullOrEmpty(options.SerialPort) && !string.IsNullOrEmpty(port))
            {
                options.SerialPort = port;
            }
            else if (!string.IsNullOrEmpty(options.SerialPort))
            {
                SettingsManager.SaveSetting(Setting.PORT, options.SerialPort);
            }
        }

        //Probably rename

        static async Task<CompletionBehavior> ProcessHcom(Options options)
        {
            if (string.IsNullOrEmpty(options.SerialPort))
            {
                Console.WriteLine("Please specify a --SerialPort");
                return CompletionBehavior.RequestFailed;
            }

            Console.WriteLine($"Opening port '{options.SerialPort}'");
            await MeadowDeviceManager.GetMeadowForSerialPort(options.SerialPort);
            // verify that the port was actually connected
            if (MeadowDeviceManager.CurrentDevice.Socket == null &&
                MeadowDeviceManager.CurrentDevice.SerialPort == null)
            {
                Console.WriteLine($"Port is unavailable.");
                return CompletionBehavior.RequestFailed;
            }

            try
            {
              if (options.WriteFile)
              {
                  if (string.IsNullOrEmpty(options.FileName))
                  {
                      Console.WriteLine($"option --WriteFile also requires option --File (the local file you wish to write)");
                  }
                  else if (!File.Exists(options.FileName))
                  {
                      Console.WriteLine($"Cannot find {options.FileName}");
                  }
                  else
                  {
                      if (string.IsNullOrEmpty(options.TargetFileName))
                      {
#if USE_PARTITIONS
                          Console.WriteLine($"Writing {options.FileName} to partition {options.Partition}");
#else
                          Console.WriteLine($"Writing {options.FileName}");
#endif
                      }
                      else
                      {
#if USE_PARTITIONS
                        Console.WriteLine($"Writing {options.FileName} as {options.TargetFileName} to partition {options.Partition}");
#else
                        Console.WriteLine($"Writing {options.FileName} as {options.TargetFileName}");
#endif
                      }
                       
                        await MeadowFileManager.WriteFileToFlash(MeadowDeviceManager.CurrentDevice, options.FileName, options.TargetFileName, options.Partition).ConfigureAwait(false); ;
                  }
              }
              else if (options.DeleteFile)
              {
                  if (string.IsNullOrEmpty(options.TargetFileName))
                  {
                      Console.WriteLine($"option --DeleteFile also requires option --TargetFileName (the file you wish to delete)");
                  }
                  else
                  {
#if USE_PARTITIONS
                      Console.WriteLine($"Deleting {options.FileName} from partion {options.Partition}");
#else
                      Console.WriteLine($"Deleting {options.FileName}");
#endif
                      await MeadowFileManager.DeleteFile(MeadowDeviceManager.CurrentDevice, options.TargetFileName, options.Partition);
                  }
              }
              else if (options.EraseFlash)
              {
                  Console.WriteLine("Erasing flash");
                  await MeadowFileManager.EraseFlash(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.VerifyErasedFlash)
              {
                  Console.WriteLine("Verifying flash is erased");
                  await MeadowFileManager.VerifyErasedFlash(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.PartitionFileSystem)
              {
                  Console.WriteLine($"Partioning file system into {options.NumberOfPartitions} partition(s)");
                  await MeadowFileManager.PartitionFileSystem(MeadowDeviceManager.CurrentDevice, options.NumberOfPartitions);
              }
              else if (options.MountFileSystem)
              {
#if USE_PARTITIONS
                  Console.WriteLine($"Mounting partition {options.Partition}");
#else
                  Console.WriteLine("Mounting file system");
#endif
                  await MeadowFileManager.MountFileSystem(MeadowDeviceManager.CurrentDevice, options.Partition);
              }
              else if (options.InitFileSystem)
              {
#if USE_PARTITIONS
                  Console.WriteLine($"Intializing filesystem in partition {options.Partition}");
#else
                  Console.WriteLine("Intializing filesystem");
#endif
                  await MeadowFileManager.InitializeFileSystem(MeadowDeviceManager.CurrentDevice, options.Partition);
              }
              else if (options.CreateFileSystem) //should this have a partition???
              {
                  Console.WriteLine($"Creating file system");
                  await MeadowFileManager.CreateFileSystem(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.FormatFileSystem)
              {
#if USE_PARTITIONS
                  Console.WriteLine($"Format file system on partition {options.Partition}");
#else
                  Console.WriteLine("Format file system");
#endif
                  await MeadowFileManager.FormatFileSystem(MeadowDeviceManager.CurrentDevice, options.Partition);
              }
              else if (options.ListFiles)
              {
#if USE_PARTITIONS
                  Console.WriteLine($"Getting list of files on partition {options.Partition}");
#else
                  Console.WriteLine($"Getting list of files");
#endif
                  await MeadowFileManager.ListFiles(MeadowDeviceManager.CurrentDevice, options.Partition);
              }
              else if (options.ListFilesAndCrcs)
              {
#if USE_PARTITIONS
                  Console.WriteLine($"Getting list of files and CRCs on partition {options.Partition}");
#else
                  Console.WriteLine("Getting list of files and CRCs");
#endif
                  await MeadowFileManager.ListFilesAndCrcs(MeadowDeviceManager.CurrentDevice, options.Partition);
              }
              //Device manager
              else if (options.SetTraceLevel)
              {
                  Console.WriteLine($"Setting trace level to {options.TraceLevel}");
                  await MeadowDeviceManager.SetTraceLevel(MeadowDeviceManager.CurrentDevice, options.TraceLevel);
              }
              else if (options.SetDeveloper1)
              {
                  Console.WriteLine($"Setting developer level to {options.DeveloperValue}");
                  await MeadowDeviceManager.SetDeveloper1(MeadowDeviceManager.CurrentDevice, options.DeveloperValue);
              }
              else if (options.SetDeveloper2)
              {
                  Console.WriteLine($"Setting developer level to {options.DeveloperValue}");
                  await MeadowDeviceManager.SetDeveloper2(MeadowDeviceManager.CurrentDevice, options.DeveloperValue);
              }
              else if (options.SetDeveloper3)
              {
                  Console.WriteLine($"Setting developer level to {options.DeveloperValue}");
                  await MeadowDeviceManager.SetDeveloper3(MeadowDeviceManager.CurrentDevice, options.DeveloperValue);
              }
              else if (options.SetDeveloper4)
              {
                  Console.WriteLine($"Setting developer level to {options.DeveloperValue}");
                  await MeadowDeviceManager.SetDeveloper4(MeadowDeviceManager.CurrentDevice, options.DeveloperValue);
              }
              else if (options.NshEnable)
              {
                  Console.WriteLine($"Enable Nsh");
                  await MeadowDeviceManager.NshEnable(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.MonoDisable)
              {
                  await MeadowDeviceManager.MonoDisable(MeadowDeviceManager.CurrentDevice);
                }
              else if (options.MonoEnable)
              {
                  await MeadowDeviceManager.MonoEnable(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.MonoRunState)
              {
                  await MeadowDeviceManager.MonoRunState(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.MonoFlash)
              {
                  await MeadowDeviceManager.MonoFlash(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.MonoUpdateRt)
              {
                    string filename = options.FileName;
                    if (string.IsNullOrEmpty(options.FileName))
                    {
                        var downloadManager = new DownloadManager();
                        var downloadedRuntimePath = Path.Combine(downloadManager.FirmwareDownloadsFilePath, downloadManager.RuntimeFilename);
                        if (File.Exists(downloadedRuntimePath))
                        {
                            Console.WriteLine("FileName not specified, falling back to latest download.");
                            filename = downloadedRuntimePath;
                        }
                    }

                    await MeadowFileManager.MonoUpdateRt(MeadowDeviceManager.CurrentDevice,
                    filename, options.TargetFileName, options.Partition);
                }
              else if (options.GetDeviceInfo)
              {
                  await MeadowDeviceManager.GetDeviceInfo(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.GetDeviceName)
              {
                  await MeadowDeviceManager.GetDeviceName(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.ResetMeadow)
              {
                  Console.WriteLine("Resetting Meadow");
                  await MeadowDeviceManager.ResetMeadow(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.EnterDfuMode)
              {
                  Console.WriteLine("Entering Dfu mode");
                  await MeadowDeviceManager.EnterDfuMode(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.TraceDisable)
              {
                  Console.WriteLine("Disabling Meadow trace messages");
                  await MeadowDeviceManager.TraceDisable(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.TraceEnable)
              {
                  Console.WriteLine("Enabling Meadow trace messages");
                  await MeadowDeviceManager.TraceEnable(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.Uart1Apps)
              {
                  Console.WriteLine("Use Uart1 for .NET Apps");
                  await MeadowDeviceManager.Uart1Apps(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.Uart1Trace)
              {
                  Console.WriteLine("Use Uart1 for outputting Meadow trace messages");
                  await MeadowDeviceManager.Uart1Trace(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.RenewFileSys)
              {
                  Console.WriteLine("Recreate a new file system on Meadow");
                  await MeadowDeviceManager.RenewFileSys(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.QspiWrite)
              {
                  Console.WriteLine($"Executing QSPI Flash Write using {options.DeveloperValue}");
                  await MeadowDeviceManager.QspiWrite(MeadowDeviceManager.CurrentDevice, options.DeveloperValue);
              }
              else if (options.QspiRead)
              {
                  Console.WriteLine($"Executing QSPI Flash Read using {options.DeveloperValue}");
                  await MeadowDeviceManager.QspiRead(MeadowDeviceManager.CurrentDevice, options.DeveloperValue);
              }
              else if (options.QspiInit)
              {
                  Console.WriteLine($"Executing QSPI Flash Initialization using {options.DeveloperValue}");
                  await MeadowDeviceManager.QspiInit(MeadowDeviceManager.CurrentDevice, options.DeveloperValue);
              }
              else if (options.StartDebugging)
              {
                  MeadowDeviceManager.StartDebugging(MeadowDeviceManager.CurrentDevice, options.VSDebugPort);
                  Console.WriteLine($"Ready for Visual Studio debugging");
                  options.KeepAlive = true;
              }
              else if (options.Esp32WriteFile)
              {
                  if (string.IsNullOrEmpty(options.FileName))
                  {
                      Console.WriteLine($"option --Esp32WriteFile requires option --File (the local file you wish to write)");
                  }
                  else
                  {
                      if (string.IsNullOrEmpty(options.TargetFileName))
                      {
                          Console.WriteLine($"Writing {options.FileName} to ESP32");
                      }
                      else
                      {
                          Console.WriteLine($"Writing {options.FileName} as {options.TargetFileName}");
                      }
                      await MeadowFileManager.WriteFileToEspFlash(MeadowDeviceManager.CurrentDevice,
                          options.FileName, options.TargetFileName, options.Partition, options.McuDestAddr);
                  }
              }
              else if (options.FlashEsp)
              {
                    var downloadManager = new DownloadManager();
                    Console.WriteLine($"Transferring {downloadManager.NetworkMeadowCommsFilename}");
                    await MeadowFileManager.WriteFileToEspFlash(MeadowDeviceManager.CurrentDevice, 
                        Path.Combine(downloadManager.FirmwareDownloadsFilePath, downloadManager.NetworkMeadowCommsFilename), mcuDestAddr: "0x10000");
                    await Task.Delay(1000);

                    Console.WriteLine($"Transferring {downloadManager.NetworkBootloaderFilename}");
                    await MeadowFileManager.WriteFileToEspFlash(MeadowDeviceManager.CurrentDevice, 
                        Path.Combine(downloadManager.FirmwareDownloadsFilePath, downloadManager.NetworkBootloaderFilename), mcuDestAddr: "0x1000");
                    await Task.Delay(1000);

                    Console.WriteLine($"Transferring {downloadManager.NetworkPartitionTableFilename}");
                    await MeadowFileManager.WriteFileToEspFlash(MeadowDeviceManager.CurrentDevice, 
                        Path.Combine(downloadManager.FirmwareDownloadsFilePath, downloadManager.NetworkPartitionTableFilename), mcuDestAddr: "0x8000");
                    await Task.Delay(1000);
                }
              else if (options.Esp32ReadMac)
              {
                  await MeadowDeviceManager.Esp32ReadMac(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.Esp32Restart)
              {
                  await MeadowDeviceManager.Esp32Restart(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.DeployApp && !string.IsNullOrEmpty(options.FileName))
              {
                    await MeadowDeviceManager.DeployApp(MeadowDeviceManager.CurrentDevice, options.FileName);
              }
             
            }
            catch (IOException ex)
            {
                if (ex.Message.Contains("semaphore"))
                {
                    Console.WriteLine("Timeout communicating with Meadow");
                }
                else
                {
                    Console.WriteLine($"Exception communicating with Meadow: {ex.Message}");
                }
                return CompletionBehavior.RequestFailed | CompletionBehavior.KeepConsoleOpen;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception communicating with Meadow: {ex.Message}");
                return CompletionBehavior.RequestFailed | CompletionBehavior.KeepConsoleOpen;
            }

            if (options.KeepAlive)
                return CompletionBehavior.Success | CompletionBehavior.KeepConsoleOpen;
            else
                return CompletionBehavior.Success | CompletionBehavior.ExitConsole;
        }
        
        //temp code until we get the device manager logic in place 
        static bool ConnectToMeadowDevice (string commPort)
		{
            var device = new MeadowSerialDevice(commPort);
            try
            {
                device.Initialize();
            }
            catch (MeadowDeviceException ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }

            MeadowDeviceManager.CurrentDevice = device;
            return true;
        }

        
    }
}
