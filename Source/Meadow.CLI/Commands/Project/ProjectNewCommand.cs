using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CliFx.Attributes;
using Meadow.CLI.Commands.DeviceManagement;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Meadow.CLI.Commands.Current.Project
{
    [Command("project new", Description = Strings.ProjectTemplates.NewCommandDescription)]
    public class ProjectNewCommand : BaseCommand<ProjectNewCommand>
    {
        [CommandOption('o', Description = Strings.ProjectTemplates.CommandOptionOutputPathDescription, IsRequired = false)]
        public string? OutputPath { get; private set; } = null;

        [CommandOption('l', Description = Strings.ProjectTemplates.CommandOptionSupportedLanguagesDescription, IsRequired = false)]
        public string Language { get; private set; } = "C#";

        public ProjectNewCommand(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
        }

        protected override async ValueTask ExecuteCommand()
        {
            AnsiConsole.MarkupLine(Strings.ProjectTemplates.WizardTitle);

            var templateList = await GetInstalledTemplates(LoggerFactory, Console, CancellationToken);

            if (templateList != null)
            {
                // Ask some pertinent questions
                var projectName = AnsiConsole.Ask<string>(Strings.ProjectTemplates.ProjectName);

                if (string.IsNullOrWhiteSpace(OutputPath))
                {
                    OutputPath = projectName;
                }

                var outputPathArgument = $"--output {OutputPath}";

                List<MeadowTemplate> selectedTemplates = GatherTemplateInformationFromUsers(templateList);

                if (selectedTemplates.Count > 0)
                {
                    await GenerateProjectsAndSolutionsFromSelectedTemplates(projectName, outputPathArgument, selectedTemplates);
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]{Strings.ProjectTemplates.NoTemplateSelected}[/]");
                }
            }
            else
            {
                Logger?.LogError(Strings.ProjectTemplates.ErrorInstallingTemplates);
            }
        }

        private List<MeadowTemplate> GatherTemplateInformationFromUsers(List<string> templateList)
        {
            var templateNames = new List<MeadowTemplate>();
            MeadowTemplate? startKitGroup = null;
            List<MeadowTemplate> startKitTemplates = new List<MeadowTemplate>();

            startKitGroup = PopulateTemplateNameList(templateList, templateNames, startKitGroup, startKitTemplates);

            var multiSelectionPrompt = new MultiSelectionPrompt<MeadowTemplate>()
                .Title(Strings.ProjectTemplates.InstalledTemplates)
                .PageSize(15)
                .NotRequired() // Can be Blank to exit
                .MoreChoicesText(string.Format($"[grey]{Strings.ProjectTemplates.MoreChoicesInstructions}[/]"))
                .InstructionsText(string.Format($"[grey]{Strings.ProjectTemplates.Instructions}[/]", $"[blue]<{Strings.Space}>[/]", $"[green]<{Strings.Enter}>[/]"))
                .UseConverter(x => x.Name);

            // I wanted StartKit to appear 1st, if it exists
            if (startKitGroup != null)
            {
                multiSelectionPrompt.AddChoiceGroup(startKitGroup, startKitTemplates);
            }

            multiSelectionPrompt.AddChoices(templateNames);

            var selectedTemplates = AnsiConsole.Prompt(multiSelectionPrompt);
            return selectedTemplates;
        }

        private async Task GenerateProjectsAndSolutionsFromSelectedTemplates(string projectName, string outputPathArgument, List<MeadowTemplate> selectedTemplates)
        {
            string generatedProjectName = projectName;

            var generateSln = AnsiConsole.Confirm(Strings.ProjectTemplates.GenerateSln);

            // Create the selected templates
            foreach (var selectedTemplate in selectedTemplates)
            {
                AnsiConsole.MarkupLine($"[green]{Strings.ProjectTemplates.CreatingProject}[/]", selectedTemplate.Name);

                var outputPath = string.Empty;
                outputPath = Path.Combine(OutputPath!, $"{OutputPath}.{selectedTemplate.ShortName}");
                outputPathArgument = "--output " + outputPath;
                generatedProjectName = $"{projectName}.{selectedTemplate.ShortName}";

                _ = await AppTools.RunProcessCommand("dotnet", $"new {selectedTemplate.ShortName} --name {generatedProjectName} {outputPathArgument} --language {Language} --force", cancellationToken: CancellationToken);
            }

            if (generateSln)
            {
                await GenerateSolution(projectName);
            }

            AnsiConsole.MarkupLine(Strings.ProjectTemplates.GenerationComplete, $"[green]{projectName}[/]");
        }

        private async Task GenerateSolution(string projectName)
        {
            AnsiConsole.MarkupLine($"[green]{Strings.ProjectTemplates.CreatingSln}[/]");

            // Create the sln
            _ = await AppTools.RunProcessCommand("dotnet", $"new sln -n {projectName} -o {OutputPath} --force", cancellationToken: CancellationToken);

            //Now add to the new sln
            var slnFilePath = Path.Combine(OutputPath!, projectName + ".sln");

            string? searchWildCard;
            switch (Language)
            {
                case "C#":
                    searchWildCard = "*.csproj";
                    break;
                case "F#":
                    searchWildCard = "*.fsproj";
                    break;
                case "VB":
                    searchWildCard = "*.vbproj";
                    break;
                default:
                    searchWildCard = "*.csproj";
                    break;
            }

            // get all the project files and add them to the sln
            var projectFiles = Directory.EnumerateFiles(OutputPath!, searchWildCard, SearchOption.AllDirectories);
            foreach (var projectFile in projectFiles)
            {
                _ = await AppTools.RunProcessCommand("dotnet", $"sln {slnFilePath} add {projectFile}", cancellationToken: CancellationToken);
            }

            await OpenSolution(slnFilePath);
        }

        private MeadowTemplate? PopulateTemplateNameList(List<string> templateList, List<MeadowTemplate> templateNameList, MeadowTemplate? startKitGroup, List<MeadowTemplate> startKitTemplates)
        {
            // Use regex to split each line into segments using two or more spaces as the separator
            var regexTemplateLines = new Regex(@"\s{2,}");

            foreach (var templatesLine in templateList)
            {
                // Isolate the long and short names
                var segments = regexTemplateLines.Split(templatesLine.Trim());
                if (segments.Length >= 2)
                {
                    // Add Key Value of Long Name and Short Name
                    var longName = segments[0].Trim();
                    var shortName = segments[1].Trim();
                    var languages = segments[2].Replace("[", string.Empty).Replace("]", string.Empty).Trim();

                    templateNameList.Add(new MeadowTemplate($"{longName} ({languages})", shortName));
                }
            }

            return startKitGroup;
        }

        internal static async Task<List<string>?> GetInstalledTemplates(ILoggerFactory loggerFactory, CliFx.Infrastructure.IConsole console, CancellationToken cancellationToken)
        {
            var templateTable = new List<string>();

            // Get the list of Meadow project templates
            var exitCode = await AppTools.RunProcessCommand("dotnet", "new list Meadow", handleOutput: outputLogLine =>
            {
                // Ignore empty output
                if (!string.IsNullOrWhiteSpace(outputLogLine))
                {
                    templateTable.Add(outputLogLine);
                }
            }, cancellationToken: cancellationToken);


            if (exitCode == 0)
            {
                if (templateTable.Count == 0)
                {
                    AnsiConsole.MarkupLine($"[yellow]{Strings.ProjectTemplates.NoTemplatesFound}[/]");

                    // Let's install the templates then
                    var projectInstallCommand = new ProjectInstallCommand(loggerFactory);
                    await projectInstallCommand.ExecuteAsync(console);

                    // Try to populate the templateTable again, after installing the templates
                    exitCode = await AppTools.RunProcessCommand("dotnet", "new list Meadow", handleOutput: outputLogLine =>
                    {
                        // Ignore empty output
                        if (!string.IsNullOrWhiteSpace(outputLogLine))
                        {
                            templateTable.Add(outputLogLine);
                        }
                    }, cancellationToken: cancellationToken);
                }

                // Extract template names from the output
                var templateNameList = templateTable
                    .Skip(4) // Skip the header information
                    .Where(line => !string.IsNullOrWhiteSpace(line)) // Avoid empty lines
                    .Select(line => line.Trim()) // Clean whitespace
                    .ToList();

                return templateNameList;
            }

            return null;
        }

        private async Task OpenSolution(string solutionPath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var exitCode = await AppTools.RunProcessCommand("cmd", $"/c start {solutionPath}");
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var exitCode = await AppTools.RunProcessCommand("code", Path.GetDirectoryName(solutionPath));
            }
            else
            {
                Logger?.LogError(Strings.UnsupportedOperatingSystem);
            }
        }
    }
}