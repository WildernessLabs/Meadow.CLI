using CommandLine;
using System;
using MeadowCLI.DeviceManagement;
using System.IO.Ports;
using System.Threading;
using System.IO;

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
                  if (options.Dfu)
                  {
                      //ToDo update to use command line args for os and user
                      DfuUpload.FlashNuttx(options.DfuOsPath, options.DfuUserPath);
                  }
                  else
                  {
                      SyncArgsCache(options);
                      behavior = ProcessHcom(options);
                  }
                }
            });

            if (System.Diagnostics.Debugger.IsAttached)
            {
                behavior = CompletionBehavior.KeepConsoleOpen;
            }

            if ((behavior & CompletionBehavior.KeepConsoleOpen) == CompletionBehavior.KeepConsoleOpen)
            {
                Console.Read();
            }
            else
            {
                Thread.Sleep(2000);
            }
        }

        static bool IsSerialPortValid(SerialPort serialPort)
        {
            if (serialPort == null)
            {
                return false;
            }

            return true;
        }

        static void SyncArgsCache(Options options)
        {
            State state = null;

            if (options.ClearCache)
            {
                StateCache.Clear();
            }
            else
            {
                state = StateCache.Load();
            }

            if (string.IsNullOrWhiteSpace(options.SerialPort))
            {
                options.SerialPort = state.SerialPort;
            }
            else
            {
                state.SerialPort = options.SerialPort;
                StateCache.Save(state);
            }
        }

        //Probably rename

        static CompletionBehavior ProcessHcom(Options options)
        {
            Console.Write($"Opening port '{options.SerialPort}'...");
            if (ConnectToMeadowDevice(options.SerialPort))
            {
                // verify that the port was actually connected
                if (MeadowDeviceManager.CurrentDevice.Socket == null &&
                    !IsSerialPortValid(MeadowDeviceManager.CurrentDevice.SerialPort))
                {
                    Console.WriteLine($"port not available");
                    return CompletionBehavior.RequestFailed;
                }
            }
            else
            {
                Console.WriteLine($"failed to open port");
                return CompletionBehavior.RequestFailed;
            }
            Console.WriteLine($"ok.");

            try
            {
              if (options.WriteFile)
              {
                  if (string.IsNullOrEmpty(options.FileName))
                  {
                      Console.WriteLine($"option --WriteFile also requires option --File (the local file you wish to write)");
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
                      MeadowFileManager.WriteFileToFlash(MeadowDeviceManager.CurrentDevice,
                          options.FileName, options.TargetFileName, options.Partition);
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
                      MeadowFileManager.DeleteFile(MeadowDeviceManager.CurrentDevice,
                      options.TargetFileName, options.Partition);
                  }
              }
              else if (options.EraseFlash)
              {
                  Console.WriteLine("Erasing flash");
                  MeadowFileManager.EraseFlash(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.VerifyErasedFlash)
              {
                  Console.WriteLine("Verifying flash is erased");
                  MeadowFileManager.VerifyErasedFlash(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.PartitionFileSystem)
              {
                  Console.WriteLine($"Partioning file system into {options.NumberOfPartitions} partition(s)");
                  MeadowFileManager.PartitionFileSystem(MeadowDeviceManager.CurrentDevice, options.NumberOfPartitions);
              }
              else if (options.MountFileSystem)
              {
#if USE_PARTITIONS
                  Console.WriteLine($"Mounting partition {options.Partition}");
#else
                  Console.WriteLine("Mounting file system");
#endif
                  MeadowFileManager.MountFileSystem(MeadowDeviceManager.CurrentDevice, options.Partition);
              }
              else if (options.InitFileSystem)
              {
#if USE_PARTITIONS
                  Console.WriteLine($"Intializing filesystem in partition {options.Partition}");
#else
                  Console.WriteLine("Intializing filesystem");
#endif
                  MeadowFileManager.InitializeFileSystem(MeadowDeviceManager.CurrentDevice, options.Partition);
              }
              else if (options.CreateFileSystem) //should this have a partition???
              {
                  Console.WriteLine($"Creating file system");
                  MeadowFileManager.CreateFileSystem(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.FormatFileSystem)
              {
#if USE_PARTITIONS
                  Console.WriteLine($"Format file system on partition {options.Partition}");
#else
                  Console.WriteLine("Format file system");
#endif
                  MeadowFileManager.FormatFileSystem(MeadowDeviceManager.CurrentDevice, options.Partition);
              }
              else if (options.ListFiles)
              {
#if USE_PARTITIONS
                  Console.WriteLine($"Getting list of files on partition {options.Partition}");
#else
                  Console.WriteLine($"Getting list of files");
#endif
                  MeadowFileManager.ListFiles(MeadowDeviceManager.CurrentDevice, options.Partition);
              }
              else if (options.ListFilesAndCrcs)
              {
#if USE_PARTITIONS
                  Console.WriteLine($"Getting list of files and CRCs on partition {options.Partition}");
#else
                  Console.WriteLine("Getting list of files and CRCs");
#endif
                  MeadowFileManager.ListFilesAndCrcs(MeadowDeviceManager.CurrentDevice, options.Partition);
              }
              //Device manager
              else if (options.SetTraceLevel)
              {
                  Console.WriteLine($"Setting trace level to {options.TraceLevel}");
                  MeadowDeviceManager.SetTraceLevel(MeadowDeviceManager.CurrentDevice, options.TraceLevel);
              }
              else if (options.SetDeveloper1)
              {
                  Console.WriteLine($"Setting developer level to {options.DeveloperValue}");
                  MeadowDeviceManager.SetDeveloper1(MeadowDeviceManager.CurrentDevice, options.DeveloperValue);
              }
              else if (options.SetDeveloper2)
              {
                  Console.WriteLine($"Setting developer level to {options.DeveloperValue}");
                  MeadowDeviceManager.SetDeveloper2(MeadowDeviceManager.CurrentDevice, options.DeveloperValue);
              }
              else if (options.SetDeveloper3)
              {
                  Console.WriteLine($"Setting developer level to {options.DeveloperValue}");
                  MeadowDeviceManager.SetDeveloper3(MeadowDeviceManager.CurrentDevice, options.DeveloperValue);
              }
              else if (options.SetDeveloper4)
              {
                  Console.WriteLine($"Setting developer level to {options.DeveloperValue}");
                  MeadowDeviceManager.SetDeveloper4(MeadowDeviceManager.CurrentDevice, options.DeveloperValue);
              }
              else if (options.NshEnable)
              {
                  Console.WriteLine($"Enable Nsh");
                  MeadowDeviceManager.NshEnable(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.MonoDisable)
              {
                  MeadowDeviceManager.MonoDisable(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.MonoEnable)
              {
                  MeadowDeviceManager.MonoEnable(MeadowDeviceManager.CurrentDevice);

                  // the device is going to reset, so we need to wait for it to reconnect
                  Console.WriteLine($"Reconnecting...");
                  System.Threading.Thread.Sleep(5000);

                  // just enter port echo mode until the user cancels
                  MeadowDeviceManager.EnterEchoMode(MeadowDeviceManager.CurrentDevice);

                  return CompletionBehavior.Success | CompletionBehavior.KeepConsoleOpen;
              }
              else if (options.MonoRunState)
              {
                  MeadowDeviceManager.MonoRunState(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.MonoFlash)
              {
                  MeadowDeviceManager.MonoFlash(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.GetDeviceInfo)
              {
                  MeadowDeviceManager.GetDeviceInfo(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.ResetMeadow)
              {
                  Console.WriteLine("Resetting Meadow");
                  MeadowDeviceManager.ResetMeadow(MeadowDeviceManager.CurrentDevice, options.DeveloperValue);
              }
              else if (options.EnterDfuMode)
              {
                  Console.WriteLine("Entering Dfu mode");
                  MeadowDeviceManager.EnterDfuMode(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.TraceDisable)
              {
                  Console.WriteLine("Disabling Meadow trace messages");
                  MeadowDeviceManager.TraceDisable(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.TraceEnable)
              {
                  Console.WriteLine("Enabling Meadow trace messages");
                  MeadowDeviceManager.TraceEnable(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.Uart1Apps)
              {
                  Console.WriteLine("Use Uart1 for .NET Apps");
                  MeadowDeviceManager.Uart1Apps(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.Uart1Trace)
              {
                  Console.WriteLine("Use Uart1 for outputting Meadow trace messages");
                  MeadowDeviceManager.Uart1Trace(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.RenewFileSys)
              {
                  Console.WriteLine("Recreate a new file system on Meadow");
                  MeadowDeviceManager.RenewFileSys(MeadowDeviceManager.CurrentDevice);
              }
              else if (options.QspiWrite)
              {
                  Console.WriteLine($"Executing QSPI Flash Write using {options.DeveloperValue}");
                  MeadowDeviceManager.QspiWrite(MeadowDeviceManager.CurrentDevice, options.DeveloperValue);
              }
              else if (options.QspiRead)
              {
                  Console.WriteLine($"Executing QSPI Flash Read using {options.DeveloperValue}");
                  MeadowDeviceManager.QspiRead(MeadowDeviceManager.CurrentDevice, options.DeveloperValue);
              }
              else if (options.QspiInit)
              {
                  Console.WriteLine($"Executing QSPI Flash Initialization using {options.DeveloperValue}");
                  MeadowDeviceManager.QspiInit(MeadowDeviceManager.CurrentDevice, options.DeveloperValue);
              }
              else if (options.VSDebugging)
              {
                   MeadowDeviceManager.VSDebugging(options.VSDebugPort);

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
                        MeadowFileManager.WriteFileToEspFlash(MeadowDeviceManager.CurrentDevice,
                            options.FileName, options.TargetFileName, options.Partition, options.McuDestAddr);
                    }
                }
                else if (options.Esp32ReadMac)
                {
                    MeadowDeviceManager.Esp32ReadMac(MeadowDeviceManager.CurrentDevice);
                }
                else if (options.Esp32Restart)
                {
                    MeadowDeviceManager.Esp32Restart(MeadowDeviceManager.CurrentDevice);
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
