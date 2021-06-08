using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MeadowCLI.Hcom;

namespace MeadowCLI.DeviceManagement
{
	public class DeploymentManager
	{
		const string GUID_EXTENSION = ".guid";

		readonly static string[] SYSTEM_FILES = { "App.exe", "System.Net.dll", "System.Net.Http.dll", "mscorlib.dll", "System.dll", "System.Core.dll", "Meadow.dll" };

		public event EventHandler<DeployStatusEventArgs> OnStatusUpdated;

		void RaiseStatus(string message, DeployStatusType type = DeployStatusType.Information)
			=> OnStatusUpdated?.Invoke(this, new DeployStatusEventArgs(message, type));

		public async Task Deploy(MeadowSerialDevice meadow, string appFolder, CancellationTokenSource cts)
		{
			if (InitializeMeadowDevice(meadow, cts) == false)
				throw new Exception("Failed to initialize Meadow");

			//run linker
			//ManualLink.LinkApp(appFolder);
			//await Task.Delay(50);//pause to release file handle

			var linkFolder = Path.Combine(appFolder, ManualLink.LinkFolder);

			var assets = GetLocalAssets(cts, appFolder);
			//var linkedFiles = GetNonSystemFiles(GetLocalAppFiles(cts, linkFolder));
			var unlinkedFiles = GetLocalAppFiles(cts, appFolder); //GetSystemFiles to isolate

			var meadowFiles = await GetFilesOnDevice(meadow, cts);

			var allFiles = new List<string>();
			allFiles.AddRange(assets.files);
			//    allFiles.AddRange(appFiles.files);
			allFiles.AddRange(unlinkedFiles.files);

			await DeleteUnusedFiles(meadow, cts, meadowFiles, allFiles);

			//deploy app
			//  await DeployFilesWithCrcCheck(meadow, cts, linkFolder, meadowFiles, appFiles);
			//  await DeployFilesWithGuidCheck(meadow, cts, linkFolder, meadowFiles, appFiles);
			await DeployFilesWithCrcCheck(meadow, cts, appFolder, meadowFiles, unlinkedFiles);

			//deploy assets
			await DeployFilesWithCrcCheck(meadow, cts, appFolder, meadowFiles, assets);

			await MeadowDeviceManager.MonoEnable(meadow).ConfigureAwait(false);
		}

		bool InitializeMeadowDevice(MeadowSerialDevice meadow, CancellationTokenSource cts)
		{
			if (cts.IsCancellationRequested) { return true; }

			RaiseStatus("Initializing Meadow");

			if (meadow == null)
			{
				RaiseStatus("Can't read Meadow device", DeployStatusType.Error);
				return false;
			}

			if (meadow.Initialize() == false)
			{
				RaiseStatus("Couldn't initialize serial port", DeployStatusType.Error);
				return false;
			}
			return true;
		}

		async Task<(List<string> files, List<UInt32> crcs)> GetFilesOnDevice(MeadowSerialDevice meadow, CancellationTokenSource cts)
		{
			if (cts.IsCancellationRequested) { return (new List<string>(), new List<UInt32>()); }

			RaiseStatus("Checking files on device (may take several seconds)");

			var meadowFiles = await meadow.GetFilesAndCrcs(30000);

			foreach (var f in meadowFiles.files)
			{
				if (cts.IsCancellationRequested)
					break;

				RaiseStatus($"Found {f}");
			}

			return meadowFiles;
		}

		(List<string> files, List<UInt32> crcs) GetLocalAppFiles(CancellationTokenSource cts, string appFolder)
		{
			var files = new List<string>();
			var crcs = new List<UInt32>();

			//crawl dependences
			//var paths = Directory.EnumerateFiles(appFolder, "*.*", SearchOption.TopDirectoryOnly);

			var dependences = AssemblyManager.GetDependencies("App.exe", appFolder);
			dependences.Add("App.exe");

			foreach (var file in dependences)
			{
				if (cts.IsCancellationRequested) { break; }

				using (FileStream fs = File.Open(Path.Combine(appFolder, file), FileMode.Open))
				{
					var len = (int)fs.Length;
					var bytes = new byte[len];

					fs.Read(bytes, 0, len);

					//0x
					var crc = CrcTools.Crc32part(bytes, len, 0);// 0x04C11DB7);

					//  Console.WriteLine($"{file} crc is {crc}");
					files.Add(Path.GetFileName(file));
					crcs.Add(crc);
				}
			}

			return (files, crcs);
		}

		(List<string> files, List<UInt32> crcs) GetLocalAssets(CancellationTokenSource cts, string assetsFolder)
		{
			//get list of files in folder
			var paths = Directory.EnumerateFiles(assetsFolder, "*.*", SearchOption.TopDirectoryOnly)
			.Where(s => //s.EndsWith(".exe") ||
						//s.EndsWith(".dll") ||
						s.EndsWith(".bmp") ||
						s.EndsWith(".jpg") ||
						s.EndsWith(".jpeg") ||
						s.EndsWith(".txt") ||
						s.EndsWith(".json") ||
						s.EndsWith(".xml") ||
						s.EndsWith(".yml")
						//s.EndsWith("Meadow.Foundation.dll")
						);

			//   var dependences = AssemblyManager.GetDependencies("App.exe" ,folder);

			var files = new List<string>();
			var crcs = new List<UInt32>();

			//crawl other files (we can optimize)
			foreach (var file in paths)
			{
				if (cts.IsCancellationRequested) break;

				using (FileStream fs = File.Open(file, FileMode.Open))
				{
					var len = (int)fs.Length;
					var bytes = new byte[len];

					fs.Read(bytes, 0, len);

					//0x
					var crc = CrcTools.Crc32part(bytes, len, 0);// 0x04C11DB7);

					// Console.WriteLine($"{file} crc is {crc}");
					files.Add(Path.GetFileName(file));
					crcs.Add(crc);
				}
			}

			return (files, crcs);
		}

		async Task DeleteUnusedFiles(MeadowSerialDevice meadow, CancellationTokenSource cts,
			(List<string> files, List<UInt32> crcs) meadowFiles, List<string> localFiles)
		{
			if (cts.IsCancellationRequested)
				return;

			foreach (var file in meadowFiles.files)
			{
				if (cts.IsCancellationRequested) { break; }

				//skip - we'll delete with the dll
				if (file.Contains(GUID_EXTENSION))
				{
					var lib = file.Substring(0, file.Length - GUID_EXTENSION.Length);
					if (localFiles.Contains(lib))
					{
						//    continue;
					}
				}

				if (localFiles.Contains(file) == false)
				{
					await MeadowFileManager.DeleteFile(meadow, file);

					RaiseStatus($"Removing {file}");
				}
			}
		}

		(List<string> files, List<UInt32> crcs) GetSystemFiles((List<string> files, List<UInt32> crcs) files)
		{
			(List<string> files, List<UInt32> crcs) systemFiles = (new List<string>(), new List<UInt32>());

			//clean this up later with a model object and linn
			for (int i = 0; i < files.files.Count; i++)
			{
				if (SYSTEM_FILES.Contains(files.files[i]))
				{
					systemFiles.files.Add(files.files[i]);
					systemFiles.crcs.Add(files.crcs[i]);
				}
			}

			return systemFiles;
		}

		(List<string> files, List<UInt32> crcs) GetNonSystemFiles((List<string> files, List<UInt32> crcs) files)
		{
			(List<string> files, List<UInt32> crcs) otherFiles = (new List<string>(), new List<UInt32>());

			//clean this up later with a model object and linn
			for (int i = 0; i < files.files.Count; i++)
			{
				if (SYSTEM_FILES.Contains(files.files[i]) == false)
				{
					otherFiles.files.Add(files.files[i]);
					otherFiles.crcs.Add(files.crcs[i]);
				}
			}

			return otherFiles;
		}

		async Task DeployFilesWithGuidCheck(
			MeadowSerialDevice meadow,
			CancellationTokenSource cts,
			string folder,
			(List<string> files, List<UInt32> crcs) meadowFiles,
			(List<string> files, List<UInt32> crcs) localFiles)
		{
			if (cts.IsCancellationRequested)
			{ return; }

			var weaver = new WeaverCRC();

			for (int i = 0; i < localFiles.files.Count; i++)
			{
				var guidFileName = localFiles.files[i] + GUID_EXTENSION;
				string guidOnMeadow = string.Empty;

				if (meadowFiles.files.Contains(guidFileName))
				{
					guidOnMeadow = await meadow.GetInitialFileData(guidFileName);
					await Task.Delay(100);
				}

				//calc guid 
				var guidLocal = weaver.GetCrcGuid(Path.Combine(folder, localFiles.files[i])).ToString();

				if (guidLocal == guidOnMeadow)
				{
					continue;
				}

				Console.WriteLine($"Guids didn't match for {localFiles.files[i]}");
				await MeadowFileManager.WriteFileToFlash(meadow, Path.Combine(folder, localFiles.files[i]), localFiles.files[i]);

				await Task.Delay(250);

				//need to write new Guid file

				var guidFilePath = Path.Combine(folder, guidFileName);
				File.WriteAllText(guidFilePath, guidLocal);

				await MeadowFileManager.WriteFileToFlash(meadow, guidFilePath, guidFileName);

				await Task.Delay(250);
			}
		}

		async Task DeployFilesWithCrcCheck(
			MeadowSerialDevice meadow,
			CancellationTokenSource cts,
			string folder,
			(List<string> files, List<UInt32> crcs) meadowFiles,
			(List<string> files, List<UInt32> crcs) localFiles)
		{
			if (cts.IsCancellationRequested)
			{ return; }

			for (int i = 0; i < localFiles.files.Count; i++)
			{
				if (meadowFiles.crcs.Contains(localFiles.crcs[i]))
				{
					// Console.WriteLine($"CRCs matched for {localFiles.files[i]}");
					continue;
				}

				// Console.WriteLine($"CRCs didn't match for {localFiles.files[i]}, {localFiles.crcs[i]:X}");
				await MeadowFileManager.WriteFileToFlash(meadow, Path.Combine(folder, localFiles.files[i]), localFiles.files[i]);

				await Task.Delay(250);
			}
		}
        
        public class DeployStatusEventArgs : EventArgs
        {
            public DeployStatusEventArgs(string message, DeployStatusType type = DeployStatusType.Information)
            {
                Message = message;
                Type = type;
            }

            public string Message { get; }
            public DeployStatusType Type { get; }
        }

        public enum DeployStatusType
        {
            Information,
            Error
        }

		public static class ManualLink
		{
			// monolinker -l all -c link -o ./linked -a ./Meadow.dll -a ./app.exe]

			public static string LinkFolder => "linked";

			public static void LinkApp(string path)
			{
				var psi = new ProcessStartInfo
				{
					WorkingDirectory = path,
					FileName = "monolinker",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					Arguments = "-l all -c link -o ./linked -a ./Meadow.dll -a ./App.exe"
				};

				//  string output = string.Empty;

				using (var p = Process.Start(psi))
				{
					if (p != null)
					{
						//    output = p.StandardOutput.ReadToEnd();
						p.WaitForExit();
					}
				}
			}
		}
	}
}
