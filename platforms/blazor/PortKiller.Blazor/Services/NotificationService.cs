using System.Collections.Concurrent;

namespace PortKiller.Blazor.Services;

public class NotificationService
{
    private readonly ConcurrentQueue<Notification> _notifications = new();
    private readonly List<Notification> _notificationHistory = new();
    private const int MaxHistorySize = 100;

    public void NotifyPortStarted(int port, string processName)
    {
        AddNotification(new Notification
        {
            Type = "success",
            Icon = "🚀",
            Message = $"端口 {port} 已启动",
            Details = $"进程: {processName}"
        });
    }

    public void NotifyPortStopped(int port)
    {
        AddNotification(new Notification
        {
            Type = "warning",
            Icon = "⚠",
            Message = $"端口 {port} 已停止",
            Details = "端口不再监听"
        });
    }

    public void NotifyTunnelCreated(int port, string url)
    {
        AddNotification(new Notification
        {
            Type = "success",
            Icon = "🌐",
            Message = $"隧道已创建: 端口 {port}",
            Details = url
        });
    }

    public void NotifyTunnelStopped(int port)
    {
        AddNotification(new Notification
        {
            Type = "info",
            Icon = "🔌",
            Message = $"隧道已停止: 端口 {port}"
        });
    }

    public void NotifyTunnelRestarted(int port)
    {
        AddNotification(new Notification
        {
            Type = "success",
            Icon = "🔄",
            Message = $"隧道已重新创建: 端口 {port}"
        });
    }

    public void NotifyPortKilled(int port, string processName)
    {
        AddNotification(new Notification
        {
            Type = "danger",
            Icon = "✕",
            Message = $"端口 {port} 已终止",
            Details = $"进程: {processName}"
        });
    }

    public void AddNotification(string type, string icon, string message, string? details = null)
    {
        AddNotification(new Notification
        {
            Type = type,
            Icon = icon,
            Message = message,
            Details = details ?? string.Empty,
            Timestamp = DateTime.Now
        });
    }

    private void AddNotification(Notification notification)
    {
        _notifications.Enqueue(notification);
        _notificationHistory.Insert(0, notification);
        
        if (_notificationHistory.Count > MaxHistorySize)
        {
            _notificationHistory.RemoveAt(_notificationHistory.Count - 1);
        }
        
        OnNotificationAdded?.Invoke(notification);
    }

    public List<Notification> GetNotificationHistory()
    {
        return _notificationHistory.ToList();
    }

    public void ClearHistory()
    {
        _notificationHistory.Clear();
    }

    public event Action<Notification>? OnNotificationAdded;
}

public class Notification
{
    public string Type { get; set; } = "info";
    public string Icon { get; set; } = "ℹ";
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
