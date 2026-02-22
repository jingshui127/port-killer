namespace PortManager.Core.Services;

/// <summary>
/// 简单日志跟踪类
/// </summary>
public static class XTrace
{
    private static readonly object _lock = new();

    public static void WriteLine(string message)
    {
        lock (_lock)
        {
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
        }
    }

    public static void LogException(Exception ex, string? message = null)
    {
        lock (_lock)
        {
            if (!string.IsNullOrEmpty(message))
            {
                Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {message}");
            }
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] EXCEPTION: {ex.Message}");
            Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] STACK: {ex.StackTrace}");
        }
    }
}
