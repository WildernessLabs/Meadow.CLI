using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using CliFx.Attributes;
using Meadow.Peripherals.Sensors.Buttons;
using Microsoft.Extensions.Logging;
using NStack;
using Terminal.Gui;

namespace Meadow.CLI.Commands.DeviceManagement;

[Command("template wizard", Description = "Gets the device's current runtime state")]
public class TemplateWizardCommand : BaseCommand<TemplateWizardCommand>
{
    Dictionary<string, string> projectTemplateMapping = new Dictionary<string, string>()
    {
        { "F7", "MeadowSKF7" },
        { "macOS", "MeadowSKMac" },
        { "ProjectLab", "MeadowSKPL" },
        { "Raspberry Pi", "MeadowSKRPi" },
        { "Windows", "MeadowSKWin" },
    };
    private string solutionOutputPath = string.Empty;

    public TemplateWizardCommand(MeadowConnectionManager connectionManager, ILoggerFactory loggerFactory)
        : base(loggerFactory)
    { }

    protected override async ValueTask ExecuteCommand()
    {
        Logger?.LogInformation($"Starting Wizard...");

        Application.Init();
        //await InstallDependencies();

        int width = 80;
        int height = 20;

        var wizard = new Wizard("Meadow Project Template Wizard")
        {
            Width = width,
            Height = height
        };

        wizard.MovingBack += (args) =>
        {
            //args.Cancel = true;
        };

        wizard.MovingNext += (args) =>
        {
            //args.Cancel = true;
        };

        wizard.Finished += (args) =>
        {
            //args.Cancel = true;
        };

        wizard.Cancelled += (args) =>
        {
            //args.Cancel = true;
        };

        // Welcome Step
        var firstStep = new Wizard.WizardStep("Welcome");
        firstStep.NextButtonText = "Continue";
        firstStep.HelpText = "This Project Wizard will ask you to provide some information about your new project.\n"
            + "Once it has all the required information it will generate a best practise solution for you to work with.\n\n"
            + "When ready Continue.";
        wizard.AddStep(firstStep);

        // Project Path Step
        var secondStep = new Wizard.WizardStep("Project Name and Path");
        wizard.AddStep(secondStep);
        secondStep.HelpText = "Please enter the project name and the location of where you would like the solution and associated projects to be created.";

        var lblProjectName = new Label() { Text = "Project Name: ", X = 1, Y = 1 };
        var txtProjectName = new TextField() { Text = "", Width = 20, X = 1, Y = Pos.Bottom(lblProjectName) };

        var lblPath = new Label() { Text = "Path: ", X = 1, Y = Pos.Bottom(txtProjectName) };
        var txtPath = new TextField() { Text = "", Width = 44, X = Pos.Left(lblPath), Y = Pos.Bottom(lblPath) };
        var btnPath = new Button() { Text = "...", Width = 8, X = Pos.Right(txtPath), Y = Pos.Top(txtPath) };

        btnPath.Clicked += () =>
        {
            var openDialog = new OpenDialog("Project Location", "Select a Directory Location", openMode: OpenDialog.OpenMode.Directory);
            Application.Run(openDialog);
            txtPath.Text = openDialog.FilePath;
        };
        secondStep.Add(lblProjectName, txtProjectName, lblPath, txtPath, btnPath);

        // Select Hardware Step
        var thirdStep = new Wizard.WizardStep("Select Hardware");
        wizard.AddStep(thirdStep);
        thirdStep.HelpText = "Please select which hardware you would like to generate projects for:";

        // Probably make this a loop at some point.
        var cbxF7 = new CheckBox() { Text = "F7", Checked = false, X = 1, Y = 1 };
        var cbxMac = new CheckBox() { Text = "macOS", Checked = false, X = 1, Y = Pos.Bottom(cbxF7) };
        var cbxProjectLab = new CheckBox() { Text = "ProjectLab", Checked = false, X = 1, Y = Pos.Bottom(cbxMac) };
        var cbxRaspberryPi = new CheckBox() { Text = "Raspberry Pi", Checked = false, X = 1, Y = Pos.Bottom(cbxProjectLab) };
        var cbxWin = new CheckBox() { Text = "Windows", Checked = false, X = 1, Y = Pos.Bottom(cbxRaspberryPi) };

        thirdStep.Add(cbxF7, cbxMac, cbxProjectLab, cbxRaspberryPi, cbxWin);

        // Add 4th step
        var fourthStep = new Wizard.WizardStep("Confirm Selection.");
        wizard.AddStep(fourthStep);
        fourthStep.HelpText = "Confirm that these are the options you selected.\n\n"
            + "If you are happy with your selection Continue or got Back and make changes.";

        var lblConfirm = new Label() { Text = "This is what you have selected so far:", X = 1, Y = 1 };
        var lbl2ProjectName = new Label() { Text = "Project Name: ", X = 1, Y = Pos.Bottom(lblConfirm) + 1 };
        var lbl2Path = new Label() { Text = "Path: ", X = 1, Y = Pos.Bottom(lbl2ProjectName) };

        ustring hardware = string.Empty;
        if (cbxF7.Checked)
            hardware += cbxF7.Text;

        if (cbxMac.Checked)
            hardware += " " + cbxMac.Text;

        if (cbxProjectLab.Checked)
            hardware += " " + cbxProjectLab.Text;

        if (cbxRaspberryPi.Checked)
            hardware += " " + cbxRaspberryPi.Text;

        if (cbxWin.Checked)
            hardware += " " + cbxWin.Text;

        var lbl2Hardware = new Label() { Text = "Hardware: ", X = 1, Y = Pos.Bottom(lbl2Path) };

        fourthStep.Add(lblConfirm, lbl2ProjectName, lbl2Path, lbl2Hardware);

        fourthStep.NextButtonText = "Create Solution";

        // Creating Solution Step
        var fifthStep = new Wizard.WizardStep("Creating Solution");
        wizard.AddStep(fifthStep);
        fifthStep.HelpText = "The wizard is complete!\n\nPress the Finish button to continue.\n\nPressing ESC will cancel the wizard.";
        var lblProgress = new Label() { Text = "Project: ", X = 1, Y = 10 };
        var pbProgress = new ProgressBar()
        {
            X = Pos.Right(lblProgress),
            Y = Pos.Top(lblProgress),
            Width = 40,
            Fraction = 0.0F
        };

        var cbxLaunchSolution = new CheckBox() { Text = "Launch Solution", Checked = true, X = 1, Y = Pos.Bottom(pbProgress) + 2 };

        fifthStep.Add(lblProgress, pbProgress, cbxLaunchSolution);

        wizard.StepChanging += (args) =>
        {
            if ((args.OldStep == secondStep)
            && (txtProjectName.Text.IsEmpty || txtPath.Text.IsEmpty))
            {
                args.Cancel = true;

                var btn = MessageBox.ErrorQuery("Project Name or Path Empty", "Both project name and path must have a value before you can continue.", "Ok");
                btnPath.EnsureFocus();
            }

            if (args.OldStep == thirdStep)
            {
                if (!cbxF7.Checked
                && !cbxRaspberryPi.Checked
                && !cbxMac.Checked
                && !cbxWin.Checked)
                {
                    args.Cancel = true;
                    var btn = MessageBox.ErrorQuery("Hardware not selected", "You must select at least 1 hardware to continue.", "Ok");
                }
                else
                {
                    lbl2ProjectName.Text = "Project Name: " + txtProjectName.Text;
                    lbl2Path.Text = "Path: " + txtPath.Text;

                    if (cbxF7.Checked)
                        hardware += "    " + cbxF7.Text + Environment.NewLine;

                    if (cbxMac.Checked)
                        hardware += "    " + cbxMac.Text + Environment.NewLine;

                    if (cbxProjectLab.Checked)
                        hardware += "    " + cbxProjectLab.Text + Environment.NewLine;

                    if (cbxRaspberryPi.Checked)
                        hardware += "    " + cbxRaspberryPi.Text + Environment.NewLine;

                    if (cbxWin.Checked)
                        hardware += "    " + cbxWin.Text + Environment.NewLine;

                    lbl2Hardware.Text = $"Hardware: {Environment.NewLine}" + hardware;
                }
            }

            if (args.NewStep == fifthStep)
            {
                //bool installationInProgress = true;
                Task.Run(async () =>
                {
                    await Task.Delay(200);

                    //while (installationInProgress)
                    {
                        lblProgress.Text = "Creating Solution : " + txtProjectName.Text;
                        solutionOutputPath = Path.Combine((string)txtPath.Text, (string)txtProjectName.Text);
                        await CreateSolution(txtProjectName.Text, solutionOutputPath);
                        pbProgress.Fraction = 0.33f;

                        // When updating from a Thread/Task always use Invoke
                        Application.MainLoop.Invoke(() =>
                        {
                            wizard.SetNeedsDisplay();
                        });

                        await Task.Delay(100);

                        // We always need Core
                        var coreProjectName = txtProjectName.Text + ".Core";
                        lblProgress.Text = "Creating Project : " + coreProjectName;
                        var coreOutputPath = Path.Combine(solutionOutputPath, (string)coreProjectName);
                        await CreateTemplate("MeadowSKC", (string)txtProjectName.Text, coreOutputPath);

                        pbProgress.Fraction = 0.66f;

                        // When updating from a Thread/Task always use Invoke
                        Application.MainLoop.Invoke(() =>
                        {
                            wizard.SetNeedsDisplay();
                        });

                        if (cbxF7.Checked)
                        {
                            // Now Create our hardware projects
                            var f7ProjectName = txtProjectName.Text + "." + cbxF7.Text;
                            lblProgress.Text = "Creating Project : " + f7ProjectName;
                            var f7OutputPath = Path.Combine(solutionOutputPath, (string)f7ProjectName);
                            await CreateTemplate(projectTemplateMapping[(string)cbxF7.Text], (string)txtProjectName.Text, f7OutputPath);
                        }

                        if (cbxMac.Checked)
                        {
                            // Now Create our hardware projects
                            var f7ProjectName = txtProjectName.Text + "." + cbxMac.Text;
                            lblProgress.Text = "Creating Project : " + f7ProjectName;
                            var f7OutputPath = Path.Combine(solutionOutputPath, (string)f7ProjectName);
                            await CreateTemplate(projectTemplateMapping[(string)cbxMac.Text], (string)txtProjectName.Text, f7OutputPath);
                        }

                        if (cbxMac.Checked)
                        {
                            // Now Create our hardware projects
                            var f7ProjectName = txtProjectName.Text + "." + cbxMac.Text;
                            lblProgress.Text = "Creating Project : " + f7ProjectName;
                            var f7OutputPath = Path.Combine(solutionOutputPath, (string)f7ProjectName);
                            await CreateTemplate(projectTemplateMapping[(string)cbxMac.Text], (string)txtProjectName.Text, f7OutputPath);
                        }

                        if (cbxRaspberryPi.Checked)
                        {
                            // Now Create our hardware projects
                            var f7ProjectName = txtProjectName.Text + "." + cbxRaspberryPi.Text;
                            lblProgress.Text = "Creating Project : " + f7ProjectName;
                            var f7OutputPath = Path.Combine(solutionOutputPath, (string)f7ProjectName);
                            await CreateTemplate(projectTemplateMapping[(string)cbxRaspberryPi.Text], (string)txtProjectName.Text, f7OutputPath);
                        }

                        if (cbxProjectLab.Checked)
                        {
                            // Now Create our hardware projects
                            var f7ProjectName = txtProjectName.Text + "." + cbxProjectLab.Text;
                            lblProgress.Text = "Creating Project : " + f7ProjectName;
                            var f7OutputPath = Path.Combine(solutionOutputPath, (string)f7ProjectName);
                            await CreateTemplate(projectTemplateMapping[(string)cbxProjectLab.Text], (string)txtProjectName.Text, f7OutputPath);
                        }

                        if (cbxWin.Checked)
                        {
                            // Now Create our hardware projects
                            var f7ProjectName = txtProjectName.Text + "." + cbxWin.Text;
                            lblProgress.Text = "Creating Project : " + f7ProjectName;
                            var f7OutputPath = Path.Combine(solutionOutputPath, (string)f7ProjectName);
                            await CreateTemplate(projectTemplateMapping[(string)cbxWin.Text], (string)txtProjectName.Text, f7OutputPath);
                        }

                        // Add all projects to the solution.
                        await AddProjectsToSolution(Path.Combine(solutionOutputPath, (string)txtProjectName.Text + ".sln"));
                        pbProgress.Fraction = 1.0f;
                    }
                });
            }
        };

        wizard.Finished += (args) =>
        {
            if (cbxLaunchSolution.Checked)
            {
                var sln = Path.Combine(solutionOutputPath, (string)txtProjectName.Text + ".sln");
                Task.Run(async () => {
                    await OpenSolution(sln);
                });
            }
        };

        Application.Top.Add(wizard);
        Application.Run(Application.Top);
    }

    private async Task InstallDependencies()
    {
        // No point installing if we don't have an internet connection
        if (NetworkInterface.GetIsNetworkAvailable())
        {
            string templateName = "Meadow";
            // Check if the package is installed
            if (!await IsTemplateInstalled(templateName))
            {
                string packageName = "WildernessLabs.Meadow.Template";

                // Install the package.
                // If an update is available it should update it automagically.
                if (!await InstallPackage(packageName))
                {
                    // Unable to install ProjectTemplates Throw Up a Message??
                }
            }
        }
    }

    private async Task CreateSolution(ustring projectName, ustring outputPath)
    {
        outputPath = outputPath
            .Replace("~", Environment.GetEnvironmentVariable("HOME"));

        var output = await StartDotNetProcess("new sln", $"--name {projectName} --output {outputPath} --force");

        //Logger?.LogInformation(output);
    }

    private async Task OpenSolution(string solutionPath)
    {

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd",
                Arguments = $"/c start {solutionPath}",
                UseShellExecute = false
            });
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Process.Start("open", solutionPath);
        }
        else
        {
            throw new Exception("Unsupported Operating System");
        }
        //Logger?.LogInformation(output);
    }

    private async Task CreateTemplate(string template, string projectName, string outputPath)
    {
        outputPath = outputPath
            .Replace("~", Environment.GetEnvironmentVariable("HOME"));

        var output = await StartDotNetProcess("new", $"{template} --name {projectName} --output {outputPath} --force");

        //Logger?.LogInformation(output);
    }

    private async Task AddProjectsToSolution(ustring solutionFile)
    {
        solutionFile = solutionFile
            .Replace("~", Environment.GetEnvironmentVariable("HOME"));

        var output = await StartDotNetProcess("sln", $"{solutionFile} add {Path.Combine(Path.GetDirectoryName((string)solutionFile) ?? string.Empty, "**/*.csproj")} ");

        //Logger?.LogInformation(output);
    }

    private async Task<bool> InstallPackage(string packageName)
    {
        return (await StartDotNetProcess("new install", packageName)).Contains(packageName);
    }

    private async Task<bool> IsTemplateInstalled(string templateName)
    {
        return (await StartDotNetProcess("new list", templateName)).Contains(templateName);
    }

    private async Task<string> StartDotNetProcess(string command, string packageName, string workingDirectory = "")
    {
        return await Task.Run(async () =>
        {
            using (var process = new System.Diagnostics.Process())
            {
                process.StartInfo.WorkingDirectory = workingDirectory;
                process.StartInfo.FileName = "dotnet";
                process.StartInfo.Arguments = $"{command} {packageName}";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();

                // To avoid deadlocks, read the output stream first and then wait
                string stdOutReaderResult;

                using (StreamReader stdOutReader = process.StandardOutput)
                {
                    stdOutReaderResult = await stdOutReader.ReadToEndAsync();
                }

                string stdErrReaderResult;

                using (StreamReader stdErrReader = process.StandardError)
                {
                    stdErrReaderResult = await stdErrReader.ReadToEndAsync();
                }

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"Command: dotnet {command} {packageName} FAILED!");
                }

                // Check if the package name exists in the output
                return stdOutReaderResult;
            }
        });
    }
}