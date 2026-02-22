namespace PortManager.Models;

/// <summary>
/// 端口访问方向
/// </summary>
public enum PortAccessDirection
{
    /// <summary>未知</summary>
    Unknown,
    /// <summary>仅入站</summary>
    Inbound,
    /// <summary>仅出站</summary>
    Outbound,
    /// <summary>双向</summary>
    Bidirectional,
    /// <summary>被阻止</summary>
    Blocked
}

/// <summary>
/// 端口防火墙规则信息
/// </summary>
public class PortFirewallInfo
{
    /// <summary>是否允许入站</summary>
    public bool AllowInbound { get; set; }
    /// <summary>是否允许出站</summary>
    public bool AllowOutbound { get; set; }
    /// <summary>入站规则名称</summary>
    public string? InboundRuleName { get; set; }
    /// <summary>出站规则名称</summary>
    public string? OutboundRuleName { get; set; }
    /// <summary>访问方向</summary>
    public PortAccessDirection Direction => (AllowInbound, AllowOutbound) switch
    {
        (true, true) => PortAccessDirection.Bidirectional,
        (true, false) => PortAccessDirection.Inbound,
        (false, true) => PortAccessDirection.Outbound,
        (false, false) => PortAccessDirection.Blocked
    };
}

public class PortInfo
{
    public int Port { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public int Pid { get; set; }
    public string Address { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsFavorite { get; set; }
    public bool IsWatched { get; set; }
    public string? ProcessType { get; set; }
    public string Protocol { get; set; } = "TCP"; // TCP 或 UDP
    
    /// <summary>防火墙规则信息</summary>
    public PortFirewallInfo? FirewallInfo { get; set; }
    
    /// <summary>访问方向（便捷属性）</summary>
    public PortAccessDirection AccessDirection => FirewallInfo?.Direction ?? PortAccessDirection.Unknown;
}
