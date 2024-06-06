using System.Reflection;
using CliFx.Attributes;
using Microsoft.Extensions.Logging;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("update", Description = Strings.Update.Description)]
public class UpdateCommand : BaseCommand<UpdateCommand>
{
	const string MEADOW_CLI = "WildernessLabs.Meadow.CLI";
	const string MEADOW_UPDATER = "Meadow.Updater";
	const string CLIFX = "CliFx";

	[CommandOption("version", 'v', IsRequired = false)]
	public string? Version { get; set; }

	public UpdateCommand(ILoggerFactory loggerFactory)
		: base(loggerFactory)
	{
	}

	protected override async ValueTask ExecuteCommand()
	{
		Logger.LogInformation(Strings.Update.Updating, MEADOW_CLI);

		string toVersion;
		if (!string.IsNullOrWhiteSpace(Version))
		{
			toVersion = $"v{Version}";
		}
		else
		{
			toVersion = "vLatest";
		}
		 ;
		Logger.LogInformation(Strings.Update.Instruction1, MEADOW_CLI, toVersion);
		Logger.LogInformation(Strings.Update.Instruction2);

		// Path to the updater executable within the tool's output directory
		string meadowUpdaterPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, $"{MEADOW_UPDATER}.dll");

		// Ensure the updater executable exists
		if (!File.Exists(meadowUpdaterPath))
		{
			Logger.LogError(Strings.Update.UpdaterNotFound);
			return;
		}

		// Copy all necessary files to a temporary location, so there aren't any access issues
		string tempUpdaterDir = Path.Combine(Path.GetTempPath(), MEADOW_UPDATER);
		if (!Directory.Exists(tempUpdaterDir))
		{
			Directory.CreateDirectory(tempUpdaterDir);
		}
		CopyMeadowUpdaterFiles(Path.GetDirectoryName(meadowUpdaterPath), tempUpdaterDir, MEADOW_UPDATER);

		// Supporting files required in the temp directory
		CopyMeadowUpdaterFiles(Path.GetDirectoryName(meadowUpdaterPath), tempUpdaterDir, CLIFX);

		string commandArguments = $"update -t {MEADOW_CLI}";
		if (!string.IsNullOrWhiteSpace(Version))
		{
			commandArguments += $" -v {Version}";
		}

		await AppTools.RunProcessCommand("dotnet", $"{Path.Combine(tempUpdaterDir, $"{MEADOW_UPDATER}.dll")} {commandArguments}", cancellationToken: CancellationToken);
	}

	internal static void CopyMeadowUpdaterFiles(string? sourceDirectory, string targetDirectory, string filesToCopy)
	{
		if (sourceDirectory == null)
		{
			return;
		}

		var toolUpdaterFiles = Directory.GetFiles(sourceDirectory, $"{filesToCopy}*");
		foreach (var file in toolUpdaterFiles)
		{
			string fileName = Path.GetFileName(file);
			string destFile = Path.Combine(targetDirectory, fileName);
			File.Copy(file, destFile, true);
		}
	}
}