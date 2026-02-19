namespace PortKiller.Blazor.Models;

public class CloudflaredStatus
{
    public bool IsInstalled { get; set; }
    public string? Version { get; set; }
    public string? LatestVersion { get; set; }
    public bool HasUpdate { get; set; }
}