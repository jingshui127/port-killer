using System;

namespace PortManager.Models;

public class Tunnel
{
    public int Port { get; set; }
    public string TunnelUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? LastError { get; set; }
    public DateTime StartTime { get; set; }
    public int ProcessId { get; set; }
    public string Uptime { get; set; } = string.Empty;
    public string TunnelName { get; set; } = string.Empty;
    public TunnelProvider Provider { get; set; } = TunnelProvider.Cloudflare;
    public bool IsActive => Status == "Active" && TunnelUrl != "Unknown";
    
    // LocalTunnel 密码（仅在 Provider 为 LocalTunnel 时使用）
    public string? Password { get; set; }

    // 链接健康状态（不保存到JSON，运行时检测）
    public string LinkStatus { get; set; } = "Unknown"; // Unknown, Checking, Online, Offline, Error
    public string? LinkStatusMessage { get; set; }
    public DateTime? LastChecked { get; set; }
}
