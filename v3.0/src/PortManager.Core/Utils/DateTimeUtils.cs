namespace PortManager.Utils;

public static class DateTimeUtils
{
    /// <summary>
    /// 获取相对时间描述（如：刚刚、5分钟前、2小时前）
    /// </summary>
    public static string GetTimeAgo(DateTime dateTime)
    {
        var timeSpan = DateTime.Now - dateTime;

        if (timeSpan.TotalMinutes < 1)
            return "刚刚";
        if (timeSpan.TotalHours < 1)
            return $"{(int)timeSpan.TotalMinutes}分钟前";
        if (timeSpan.TotalDays < 1)
            return $"{(int)timeSpan.TotalHours}小时前";

        return $"{(int)timeSpan.TotalDays}天前";
    }

    /// <summary>
    /// 获取运行时间描述
    /// </summary>
    public static string GetUptime(DateTime startTime)
    {
        var uptime = DateTime.Now - startTime;
        
        if (uptime.TotalDays >= 1)
            return $"{(int)uptime.TotalDays}天{uptime.Hours}小时";
        if (uptime.TotalHours >= 1)
            return $"{(int)uptime.TotalHours}小时{uptime.Minutes}分钟";
        
        return $"{(int)uptime.TotalMinutes}分钟";
    }
}
