﻿namespace Meadow.CLI;

public static class Strings
{
    public const string ErrorNoMeadowFound = "No connected meadow device found";
    public const string GettingDeviceClock = "Getting device clock...";
    public const string SettingDeviceClock = "Setting device clock...";
    public const string InvalidApplicationPath = "Invalid application path";
    public const string InvalidParameter = "Invalid parameter";
    public const string GettingDeviceInfo = "Getting device info";
    public const string GettingDevicePublicKey = "Getting device public key";
    public const string UnableToGetDeviceInfo = "Unable to get device info";
    public const string RetrievingUserAndOrgInfo = "Retrieving your user and organization information...";
    public const string MemberOfMoreThanOneOrg = "You are a member of more than 1 organization. Please specify the desired orgId for this device provisioning.";
    public const string UnableToFindMatchingOrg = "Unable to find an organization with a Name or ID matching '{0}'";
    public const string MustBeSignedInRunMeadowLogin = "You must be signed into your Wilderness Labs account to execute this command. Run 'meadow login' to do so.";
    public const string RequestingDevicePublicKey = "Requesting device public key (this will take a minute)...";
    public const string CouldNotRetrievePublicKey = "Could not retrieve device's public key";
    public const string DeviceReturnedInvalidPublicKey = "Device returned an invalid public key";
    public const string ProvisioningWithCloud = "Provisioning device with Meadow.Cloud...";
    public const string ProvisioningSucceeded = "Device provisioned successfully";
    public const string ProvisioningFailed = "Failed to provision device: {0}";
    public const string BuildingSpecifiedConfiguration = "Building {0} configuration of application...";
    public const string AppBuildFailed = "Application build failed";
    public const string TrimmingApplicationForSpecifiedVersion = "Trimming application for OS version {0}...";
    public const string TrimmedApplicationForSpecifiedVersion = "Trimmed application with OS version {0}";
    public const string AssemblingCloudPackage = "Assembling the MPAK...";
    public const string PackageAssemblyFailed = "Package assembly failed";
    public const string PackageAvailableAtSpecifiedPath = "Package is available at {0}";
    public const string NoCompiledApplicationFound = "No compiled application found";
    public const string DfuDeviceDetected = "DFU Device Detected";
    public const string UsingDfuToWriteOs = "using DFU to write OS";
    public const string NoDfuDeviceDetected = "No DFU Device Detected. Power the device with the BOOT button pressed.";
    public const string OsFileNotFoundForSpecifiedVersion = "OS file not found for version '{0}'";
    public const string WritingAllFirmwareForSpecifiedVersion = "Writing all firmware for version '{0}'";
    public const string DisablingRuntime = "Disabling runtime";
    public const string DisablingTracing = "Disabling tracing";
    public const string EnablingRuntime = "Enabling runtime";
    public const string EnablingTracing = "Enabling tracing";
    public const string Writing = "Writing";
    public const string InvalidFirmwareForSpecifiedPath = "Invalid firmware path '{0}'";
    public const string UnknownSpecifiedFirmwareFile = "Unknown firmware file '{0}'";
    public const string WritingSpecifiedFirmwareFile = "Writing firmware file '{0}'";
    public const string DfuWriteFailed = "DFU write failed";
    public const string FirmwareUpdatedSuccessfully = "Firmware updated successfully";
    public const string BuildConfiguration = "The build configuration";
    public const string PathMeadowApplication = "Path to the Meadow application";
    public const string PathToMeadowProject = "Path to the Meadow project file";
    public const string NoLinkAssemblies = "Assemblies to skip during linking";
    public const string WritingCoprocessorFiles = "Writing Coprocessor files";
    public const string MeadowFoundAt = "Meadow found at";
    public const string GettingRuntimeFor = "Getting runtime for";
    public const string WritingRuntime = "Writing runtime";
    public const string ErrorWritingRuntime = "Error writing runtime for";
    public const string Retrying = "retrying";
    public const string MultipleDfuDevicesFound = "Multiple devices found in bootloader mode - only connect one device";
    public const string SpecifiedFirmwareVersionNotFound = "Requested firmware version '{0}' not found";
    public const string NoDefaultVersionSet = "No default version set";
    public const string UseCommandFirmwareDefault = "Use 'meadow firmare default' to set a default version";
    public const string FirmwareWriteFailed = "Firmware write failed";
    public const string UnableToReadSerialNumber = "unable to read device serial number";
    public const string MakeSureDeviceisConnected = "make sure a device is connected";
    public const string IsDfuUtilInstalled = "Is dfu-util installed?";
    public const string RunMeadowDfuInstall = "Run 'meadow dfu install' to install";
    public const string NewMeadowDeviceNotFound = "New Meadow device not found";
    public const string NoFirmwarePackagesFound = "No firmware packages found, run 'meadow firmware download' to download the latest firmware";
    public const string NoDefaultFirmwarePackageSet = "No default firmware package set, run 'meadow firmware default' to set the default firmware";
    public const string AppDeployFailed = "Application deploy failed";
    public const string AppDeployedSuccessfully = "Application deployed successfully";
    public const string AppTrimFailed = "Application trimming failed";
    public const string WithConfiguration = "with configuration";
    public const string At = "at";
    public const string DfuUsbErrorMessage = "Found a DFU device but couldn't identify it as Meadow hardware. \r\nEnsure your device is in DFU mode and is correctly recognized by the host development machine.";

    public static class Telemetry
    {
        public const string ConsentMessage = @"
Let's improve the Meadow experience together
--------------------------------------------
To help improve the Meadow experience, we'd like to collect anonymous usage data. This data helps us understand how our tools are used, so we can make them better for everyone. This usage data is not tied to individuals and no personally identifiable information is collected.

Our privacy policy is available at https://www.wildernesslabs.co/privacy-policy.

You can change your mind at any time by running the ""{0}"" command or by setting the {1} environment variable to '1' or '0' ('true' or 'false', respectively).
";
        public const string AskToParticipate = "Would you like to participate?";
    }

    public static class ProjectTemplates
    {
        public const string InstallTitle = "Installing and updating the Meadow Project Templates";
        public const string Installed = "The following Meadow Project Templates are now installed.";

        public const string WizardTitle = "Meadow Project Wizard...";
        public const string ProjectName = "What is your project's name?";
        public const string GenerationComplete = "All of {0}'s projects have now been created!";
        public const string GenerateSln = "Create an sln that contains all the new projects?";
        public const string NoTemplatesFound = "No Meadow project templates found.";
        public const string InstalledTemplates = "--- Installed Templates ---";
        public const string MoreChoicesInstructions = "(Move up and down to reveal more templates)";
        public const string Instructions = "Press {0} to toggle a template or {1} to accept and generate the selected templates";
        public const string CreatingProject = "Creating {0} project";
        public const string CreatingSln = "Creating sln for selected projects....";
        public const string NoTemplateSelected = "No templates selected.";
        public const string NewCommandDescription = "Shows a list of project templates to choose from.";
        public const string CommandOptionOutputPathDescription = "The path where you would like the projects created.";
        public const string CommandOptionSupportedLanguagesDescription = "Generate the projects using the specified language. Valid values are C# or F# or VB.NET";
        public const string InstallCommandDescription = "Install or updates to the latest project templates";
        public const string ErrorInstallingTemplates = "An error occured install the templates. Check that your internet connection is working.";
        public const string ColumnTemplateName = "Template Name";
        public const string ColumnLanguages = "Languages";
    }

    public const string UnsupportedOperatingSystem = "Unsupported Operating System";
    public const string Enter = "Enter";
    public const string Space = "Space";

    public static class FirmwareUpdater
    {
        public const string FlashingOS = "Flashing OS";
        public const string WritingRuntime = "Writing Runtime";
        public const string WritingESP = "Writing ESP";
        public const string SwitchingToLibUsbClassic = "This machine requires an older version of LibUsb. The CLI settings have been updated, re-run the 'firmware write' command to update your device.";
    }
}