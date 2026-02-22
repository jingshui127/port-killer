using Microsoft.Extensions.Logging;

namespace NewLife.Log;

/// <summary>
/// XTrace 兼容层，用于替换 NewLife.Log.XTrace
/// </summary>
public static class XTrace
{
    private static ILogger? _logger;
    
    public static void SetLogger(ILogger logger)
    {
        _logger = logger;
    }
    
    public static void WriteLine(string message)
    {
        _logger?.LogInformation(message);
    }
    
    // 内部 Log 类，避免与 XTrace.Log 方法冲突
    public static class Logger
    {
        public static void Debug(string message) => _logger?.LogDebug(message);
        public static void Info(string message) => _logger?.LogInformation(message);
        public static void Warn(string message) => _logger?.LogWarning(message);
        public static void Error(string message) => _logger?.LogError(message);
    }
}

public enum Level
{
    Debug,
    Info,
    Warn,
    Error
}
