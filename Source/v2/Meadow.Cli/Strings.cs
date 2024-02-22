namespace Meadow.CLI;

public static class Strings
{
    public const string ErrorNoMeadowFound = "No connected meadow device found";
    public const string GettingDeviceClock = "Getting device clock...";
    public const string SettingDeviceClock = "Setting device clock...";
    public const string InvalidApplicationPath = "Invalid application path";
    public const string InvalidParameter = "Invalid parameter";
    public const string GettingDeviceInfo = "Getting device info...";
    public const string RetrievingUserAndOrgInfo = "Retrieving your user and organization information...";
    public const string MemberOfMoreThanOneOrg = "You are a member of more than 1 organization. Please specify the desired orgId for this device provisioning.";
    public const string UnableToFindMatchingOrg = "Unable to find an organization with a Name or ID matching '{0}'";
    public const string MustBeSignedInRunMeadowLogin = $"You must be signed in to execute this command.\n"
         + "Please run \"meadow cloud login\" to sign in to your Wilderness Labs account.";
    public const string RequestingDevicePublicKey = "Requesting device public key (this will take a minute)...";
    public const string CouldNotRetrievePublicKey = "Could not retrieve device's public key";
    public const string DeviceReturnedInvalidPublicKey = "Device returned an invalid public key";
    public const string ProvisioningWithCloud = "Provisioning device with Meadow.Cloud...";
    public const string ProvisioningSucceeded = "Device provisioned successfully";
    public const string ProvisioningFailed = "Failed to provision device: {0}";
    public const string BuildingSpecifiedConfiguration = "Building {0} configuration of application...";
    public const string BuildFailed = "Build failed";
    public const string TrimmingApplicationForSpecifiedVersion = "Trimming application for OS version {0}...";
    public const string AssemblingCloudPackage = "Assembling the MPAK...";
    public const string PackageAssemblyFailed = "Package assembly failed";
    public const string PackageAvailableAtSpecifiedPath = "Done. Package is available at {0}";
    public const string NoCompiledApplicationFound = "No compiled application found";
}
