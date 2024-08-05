namespace Meadow.CLI.Commands.Provision;

public class ProvisionSettings
{
    public string? AppPath { get; set; }
    public string? Configuration { get; set; }
    public bool? DeployApp { get; set; }
    public string? FirmwareVersion { get; set; }
}