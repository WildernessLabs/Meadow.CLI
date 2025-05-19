using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CliFx.Attributes;
using Meadow.CLI.Commands.DeviceManagement;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Meadow.CLI.Commands.Current.Project
{
	[Command("project install", Description = Strings.ProjectTemplates.InstallCommandDescription)]
	public class ProjectInstallCommand : BaseCommand<ProjectInstallCommand>
	{
        public ProjectInstallCommand(ILoggerFactory loggerFactory)
        : base(loggerFactory)
        {
        }

        protected override async ValueTask ExecuteCommand()
        {
            AnsiConsole.MarkupLine(Strings.ProjectTemplates.InstallTitle);

            var templateList = await ProjectNewCommand.GetInstalledTemplates(LoggerFactory, Console, CancellationToken);

            if (templateList != null)
            {
                DisplayInstalledTemplates(templateList);
            }
            else
            {
                Logger?.LogError(Strings.ProjectTemplates.ErrorInstallingTemplates);
            }
        }

        private void DisplayInstalledTemplates(List<string> templateList)
        {
            // Use regex to split each line into segments using two or more spaces as the separator
            var regex = new Regex(@"\s{2,}");

            var table = new Table();
            // Add some columns
            table.AddColumn(Strings.ProjectTemplates.ColumnTemplateName);
            table.AddColumn(Strings.ProjectTemplates.ColumnLanguages);
            foreach (var templatesLine in templateList)
            {
                // Isolate the long and shortnames, as well as languages
                var segments = regex.Split(templatesLine.Trim());
                if (segments.Length >= 2)
                {
                    // Add Key Value of Long Name and Short Name
                    var longName = segments[0].Trim();
                    var languages = segments[2].Replace("[", string.Empty).Replace("]", string.Empty).Trim();
                    table.AddRow(longName, languages);
                }
            }
            AnsiConsole.WriteLine(Strings.ProjectTemplates.Installed);

            // Render the table to the console
            AnsiConsole.Write(table);
        }
    }
}