using System;

namespace PortManager.Models;

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
    public bool IsActive => Status == "Active" && TunnelUrl != "Unknown";
    
    // 链接健康状态（不保存到JSON，运行时检测）
    public string LinkStatus { get; set; } = "Unknown"; // Unknown, Checking, Online, Offline, Error
    public string? LinkStatusMessage { get; set; }
    public DateTime? LastChecked { get; set; }
}

