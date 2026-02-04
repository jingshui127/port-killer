using System;

namespace PortKiller.Blazor.Models;

public class CloudflareTunnel
{
    public int Port { get; set; }
    public string TunnelUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? LastError { get; set; }
    public DateTime StartTime { get; set; }
    public int ProcessId { get; set; }
    public string Uptime { get; set; } = string.Empty;
    public string TunnelName { get; set; } = string.Empty;
    public bool IsActive => Status == "Active";
}
