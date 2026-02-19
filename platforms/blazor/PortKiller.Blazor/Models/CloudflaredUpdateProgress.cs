namespace PortKiller.Blazor.Models;

public class CloudflaredUpdateProgress
{
    public string Status { get; set; } = "idle";
    public string Message { get; set; } = "";
    public int Progress { get; set; }
    public string? Version { get; set; }
    public bool IsComplete { get; set; }
    public bool IsError { get; set; }
}
