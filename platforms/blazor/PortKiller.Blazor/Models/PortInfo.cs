namespace PortKiller.Blazor.Models;

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
}
