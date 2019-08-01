using System;
using System.IO;
using System.IO.Ports;

namespace MeadowCLI.Hcom
{
	public class InitializeApp
	{
        public const int MaxTargetFileNameLength = 32;

        // WARNING - Don't reuse the following in production code. It thrown together and
        // is poorly designed.
        public void parseCommandLine(string[] args, ref TargetParsedArguments targetParsedArgs)
		{
			for (int i = 0; i < args.Length; i++)
			{
				string argsLower = args[i].ToLower();
				switch (argsLower)
				{
					case "--flashextfile":
						// Expecting: -file <filename> -port <commPort> -targetFileName <destFileName> -partition <partition number>
						if (!OpenRequiredPort(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						if (!FindFileName(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						if (!FindTargetFileName(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						if (!FindPartition(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						targetParsedArgs.MeadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_FILE_TRANSFER;
						break;

					case "--deletefilebyname":
						// Expecting: -port <commPort> -targetFileName <destFileName> -partition <partition number>
						if (!OpenRequiredPort(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						if (!FindTargetFileName(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						if (!FindPartition(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						targetParsedArgs.MeadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DELETE_FILE_BY_NAME;
						break;

					case "--bulkeraseflash":
						// Expecting: -port <commPort> 
						if (!OpenRequiredPort(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						targetParsedArgs.MeadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_BULK_FLASH_ERASE;
						break;

					case "--verifyerasedflash":
						// Expecting: -port <commPort> 
						if (!OpenRequiredPort(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						targetParsedArgs.MeadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_VERIFY_ERASED_FLASH;
						break;

					case "--partitionflashfilesys":
						// Expecting: -port <commPort> 
						if (!OpenRequiredPort(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						if (!FindUserData(args, ref targetParsedArgs, true, "-numbpartitions"))
						{
							ShowUsage();
							return;
						}
						targetParsedArgs.MeadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_PARTITION_FLASH_FS;
						break;
						
					case "--mountflashfilesys":
						// Expecting: -port <commPort> 
						if (!OpenRequiredPort(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						if (!FindPartition(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						targetParsedArgs.MeadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_MOUNT_FLASH_FS;
						break;

					case "--initializeflashfilesys":
						// Expecting: -port <commPort> 
						if (!OpenRequiredPort(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						if (!FindPartition(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						targetParsedArgs.MeadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_INITIALIZE_FLASH_FS;
						break;

					case "--createentireflashfilesys":
						// Expecting: -port <commPort> 
						if (!OpenRequiredPort(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						if (!FindUserData(args, ref targetParsedArgs, true, "-numbpartitions"))
						{
							ShowUsage();
							return;
						}
						targetParsedArgs.MeadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_CREATE_ENTIRE_FLASH_FS;
						break;

					case "--formatflashfilesys":
						// Expecting: -port <commPort> 
						if (!OpenRequiredPort(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						if(!FindPartition(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						targetParsedArgs.MeadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_FORMAT_FLASH_FILE_SYS;
						break;

					// This is kind of ugly providing a numeric (0 = none, 1 = info and 2 = debug)
					case "--changetracelevel":
						// Expecting: -port <commPort> -newLevel
						if (!OpenRequiredPort(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						if (!FindUserData(args, ref targetParsedArgs, true, "-newtracelevel"))
						{
							ShowUsage();
							return;
						}
						targetParsedArgs.MeadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_CHANGE_TRACE_LEVEL;
						break;

					case "--resettargetmcu":
						// Expecting: -port <commPort> 
						if (!OpenRequiredPort(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						targetParsedArgs.MeadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_RESET_PRIMARY_MCU;
						break;

					case "--enterdfumode":
						// Expecting: -port <commPort> 
						if (!OpenRequiredPort(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						targetParsedArgs.MeadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENTER_DFU_MODE;
						break;

					case "--enabledisablensh":
						// Expecting: -port <commPort> 
						if (!OpenRequiredPort(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						if (!FindUserData(args, ref targetParsedArgs, true, "-enable"))
						{
							ShowUsage();
							return;
						}
						targetParsedArgs.MeadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH;
						break;

					case "--rqstlistoffiles":
						// Expecting: -port <commPort> 
						if (!OpenRequiredPort(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						if (!FindPartition(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						targetParsedArgs.MeadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_LIST_PARTITION_FILES;
						break;

					case "--rqstlistoffilescrc":
						// Expecting: -port <commPort> 
						if (!OpenRequiredPort(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						if (!FindPartition(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						targetParsedArgs.MeadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_LIST_PART_FILES_AND_CRC;
						break;

					case "--developer1":
						// Expecting: -port <commPort> 
						if (!OpenRequiredPort(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						if (!FindUserData(args, ref targetParsedArgs, false, "-userdata"))
						{
							ShowUsage();
							return;
						}
						targetParsedArgs.MeadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_1;
						break;

					case "--developer2":
						// Expecting: -port <commPort> 
						if (!OpenRequiredPort(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						if (!FindUserData(args, ref targetParsedArgs, true, "-userdata"))
						{
							ShowUsage();
							return;
						}
						targetParsedArgs.MeadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_2;
						break;
						
					case "--developer3":
						// Expecting: -port <commPort> 
						if (!OpenRequiredPort(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						if (!FindUserData(args, ref targetParsedArgs, true, "-userdata"))
						{
							ShowUsage();
							return;
						}
						targetParsedArgs.MeadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_3;
						break;

					case "--developer4":
						// Expecting: -port <commPort> 
						if (!OpenRequiredPort(args, ref targetParsedArgs))
						{
							ShowUsage();
							return;
						}
						if (!FindUserData(args, ref targetParsedArgs, true, "-userdata"))
						{
							ShowUsage();
							return;
						}
						targetParsedArgs.MeadowRequestType = HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_4;
						break;

					default:		// Ignonre all other tokens
						break;
				}
			}

			if (!CheckRequiredArguments(targetParsedArgs))
			{
				ShowUsage();
			}
		}

		//----------------------------------------------------------------
		// Verify we have everything needed
		bool CheckRequiredArguments(TargetParsedArguments targetParsedArgs)
		{
			if (targetParsedArgs.SerialPort == null)
			{
				Console.WriteLine("To communicate with the target MCU, a serial port must be provided (e.g. Com1");
				return false;
			}

			switch (targetParsedArgs.MeadowRequestType)
			{
				// Nothing to test
				case HcomMeadowRequestType.HCOM_MDOW_REQUEST_BULK_FLASH_ERASE:
					break;
				case HcomMeadowRequestType.HCOM_MDOW_REQUEST_VERIFY_ERASED_FLASH:
					break;
				case HcomMeadowRequestType.HCOM_MDOW_REQUEST_RESET_PRIMARY_MCU:
					break;
				case HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENTER_DFU_MODE:
					break;
				case HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_1:
					break;
				case HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_2:
					break;
				case HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_3:
					break;
				case HcomMeadowRequestType.HCOM_MDOW_REQUEST_DEVELOPER_4:
					break;

				case HcomMeadowRequestType.HCOM_MDOW_REQUEST_START_FILE_TRANSFER:
					if (string.IsNullOrEmpty(targetParsedArgs.FileName) ||
						string.IsNullOrEmpty(targetParsedArgs.TargetFileName) || targetParsedArgs.IsUserDataSet)
					{
						Console.WriteLine("To add a file to the targets flash, a valid file, path and partition must be provided (e.g. c:/myfile.dll");
						return false;
					}
					break;
				case HcomMeadowRequestType.HCOM_MDOW_REQUEST_PARTITION_FLASH_FS:
					if(targetParsedArgs.IsUserDataSet)
					{
						Console.WriteLine("To partition the file system, the number of partitions must be provided. Value 1 - 8.");
						return false;
					}
					break;
				case HcomMeadowRequestType.HCOM_MDOW_REQUEST_INITIALIZE_FLASH_FS:
					if (targetParsedArgs.IsUserDataSet)
					{
						Console.WriteLine("To initialize the file system, the partition must be provided. Values -1, 0 - 7.");
						return false;
					}
					break;
				case HcomMeadowRequestType.HCOM_MDOW_REQUEST_CREATE_ENTIRE_FLASH_FS:
					if (targetParsedArgs.IsUserDataSet)
					{
						Console.WriteLine("To create the file system, the number of partitions must be provided. Value 1 - 8.");
						return false;
					}
					break;

				case HcomMeadowRequestType.HCOM_MDOW_REQUEST_MOUNT_FLASH_FS:
					if(targetParsedArgs.IsUserDataSet)
					{
						Console.WriteLine("To mount the file system, the partition must be provided. Values -1, 0 - 7.");
						return false;
					}
					break;
				case HcomMeadowRequestType.HCOM_MDOW_REQUEST_FORMAT_FLASH_FILE_SYS:
					if(targetParsedArgs.IsUserDataSet)
					{
						Console.WriteLine("To format the file system, the partition must be provided. Values -1, 0 - 7.");
						return false;
					}
					break;

				case HcomMeadowRequestType.HCOM_MDOW_REQUEST_DELETE_FILE_BY_NAME:
					if (string.IsNullOrEmpty(targetParsedArgs.TargetFileName))
					{
						Console.WriteLine("To delete a file to the target's flash, a valid file and path must be provided (e.g. c:/myfile.dll");
						return false;
					}
					if (targetParsedArgs.IsUserDataSet)
					{
						Console.WriteLine("To file by name, the partition must be provided. Values 0 - 7");
						return false;
					}
					break;
				case HcomMeadowRequestType.HCOM_MDOW_REQUEST_CHANGE_TRACE_LEVEL:
					if (targetParsedArgs.IsUserDataSet)
					{
						Console.WriteLine("To change the trace level, the new level must be provided. Values 0 - 3.");
						return false;
					}
					break;
				case HcomMeadowRequestType.HCOM_MDOW_REQUEST_ENABLE_DISABLE_NSH:
					if (targetParsedArgs.IsUserDataSet)
					{
						Console.WriteLine("To enable or disable NuttShell, a valid value must be provided. Value 1 or 0.");
						return false;
					}
					break;

				case HcomMeadowRequestType.HCOM_MDOW_REQUEST_LIST_PARTITION_FILES:
					if (targetParsedArgs.IsUserDataSet)
					{
						Console.WriteLine("To request a list of files a valid partition must be provided. Value 0 - 7.");
						return false;
					}
					break;

				case HcomMeadowRequestType.HCOM_MDOW_REQUEST_LIST_PART_FILES_AND_CRC:
					if (targetParsedArgs.IsUserDataSet)
					{
						Console.WriteLine("To request a list of files a valid partition must be provided. Value 0 - 7.");
						return false;
					}
					break;

				default:
					{
						throw new InvalidOperationException(string.Format("Unknown request: {0}", targetParsedArgs.MeadowRequestType));
					}
			}
			return true;
		}

		//----------------------------------------------------------------
		void ShowUsage()
		{
			Console.WriteLine("Command Line stuff not right. Hit any key to exit.");
			Environment.Exit(1);
		}

		//----------------------------------------------------------------
		bool FindPartition(string[] args, ref TargetParsedArguments targetParsedArgs)
		{
			if (!FindUserData(args, ref targetParsedArgs, true, "--partition"))
				targetParsedArgs.UserData = 0;

			return true;
		}


		//----------------------------------------------------------------
		bool FindUserData(string[] args, ref TargetParsedArguments targetParsedArgs, bool isNumber, string argKey)
		{
			// return if data already set
			if (!targetParsedArgs.IsUserDataSet)
				return true;

			for (int i = 0; i < args.Length; i++)
			{
				string argsLower = args[i].ToLower();
				if (argsLower == argKey)
				{
					if(isNumber)
						targetParsedArgs.UserData = Convert.ToUInt32(args[i + 1]);
					else
						targetParsedArgs.UserData = args[i + 1][0];

					return true;
				}
			}
			return false;
		}

		//----------------------------------------------------------------
		// returns false if not found
		bool FindFileName(string[] args, ref TargetParsedArguments targetParsedArgs)
		{
			if (!string.IsNullOrEmpty(targetParsedArgs.FileName))
				return true;

			string filename = string.Empty;

			// -file <filename>
			for (int i = 0; i < args.Length; i++)
			{
				string argsLower = args[i].ToLower();
				if (argsLower == "-file")
				{
					// Assumes file path has no spaces
					filename = args[i + 1];
					if (File.Exists(filename))
					{
						targetParsedArgs.FileName = filename;	// save
						return true;
					}
					else
					{
						Console.WriteLine("Specified file '{0}' could not be found.", filename);
						return false;
					}
				}
			}
			return false;
		}

		//----------------------------------------------------------------
		// returns false if not found
		bool FindTargetFileName(string[] args, ref TargetParsedArguments targetParsedArgs)
		{
			if (!string.IsNullOrEmpty(targetParsedArgs.TargetFileName))
				return true;

			string targetFileName = string.Empty;

			// -targetFileName <targetFileName>
			for (int i = 0; i < args.Length; i++)
			{
				string argsLower = args[i].ToLower();
				if (argsLower == "-targetfilename")
				{
					targetFileName = args[i + 1];
					if (targetFileName.IndexOf("-") == -1)
					{
						targetParsedArgs.TargetFileName = targetFileName; // save
						if(targetFileName.Length > MaxTargetFileNameLength)
						{
							Console.WriteLine("The length {0} of '{1}' cannot exceed {2} characters.",
								targetFileName.Length, targetFileName, MaxTargetFileNameLength);
							return false;
						}
						return true;
					}
					else
					{
						Console.WriteLine("MCU file name not found in command tail.", targetFileName);
						return false;
					}
				}
			}

			if (!string.IsNullOrEmpty(targetParsedArgs.FileName))
			{
				targetParsedArgs.TargetFileName = Path.GetFileName(targetParsedArgs.FileName);
				return true;
			}

			return false;
		}

        //----------------------------------------------------------------
        // returns false if not found
        bool OpenRequiredPort(string[] args, ref TargetParsedArguments targetParsedArgs)
        {
            if (targetParsedArgs.SerialPort != null)
                return true;

            // -port <commPort>
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].ToLower() == "-port")
                {
                    // Assumes port defn has no spaces (e.g. Com19 or ttymodem3453454)
                    targetParsedArgs.SerialPortName = args[i + 1];
                }
            }

            if (string.IsNullOrWhiteSpace(targetParsedArgs.SerialPortName))
            {
                targetParsedArgs.SerialPortName = "/dev/tty.usbmodem01";
            }

            SerialPort serialPort;

            if (OpenSerialPort(targetParsedArgs.SerialPortName, out serialPort))
            {
                targetParsedArgs.SerialPort = serialPort;
                return true;
            }

            return false;   // port not found
        }

        //----------------------------------------------------------------
        // returns false if fails to open port
        bool OpenSerialPort(string portName, out SerialPort serialPort)
		{
			serialPort = null;
			try
			{
				// Create a new SerialPort object with default settings.
				serialPort = new SerialPort();
				serialPort.PortName = portName;
				serialPort.BaudRate = 115200;		// This value is ignored when using ACM
				serialPort.Parity = Parity.None;
				serialPort.DataBits = 8;
				serialPort.StopBits = StopBits.One;
				serialPort.Handshake = Handshake.None;

				// Set the read/write timeouts
				serialPort.ReadTimeout = 500;
				serialPort.WriteTimeout = 500;

				serialPort.Open();
				Console.WriteLine("Port: {0} opened", portName);
				return true;
			}
			catch (IOException ioe)
			{
				Console.WriteLine("The specified port '{0}' could not be found or opened. {1}Exception:'{2}'",
					portName, Environment.NewLine, ioe);
				throw;
			}
			catch (Exception except)
			{
				Console.WriteLine("Unknown exception:{0}", except);
				throw;
			}
		}
	}
}