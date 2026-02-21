using PortKiller.Blazor.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace PortKiller.Blazor.Services;

public class PortScannerService
{
    private readonly List<PortInfo> _ports = new();
    private readonly object _lock = new();
    private readonly SettingsService _settingsService;
    private readonly NotificationService _notificationService;
    private readonly ILogger<PortScannerService> _logger;

    public PortScannerService(SettingsService settingsService, NotificationService notificationService, ILogger<PortScannerService> logger)
    {
        _settingsService = settingsService;
        _notificationService = notificationService;
        _logger = logger;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var favorites = _settingsService.GetFavorites();
        var watchedPorts = _settingsService.GetWatchedPorts().Select(w => w.Port).ToHashSet();
        
        foreach (var port in favorites)
        {
            _favorites[port] = true;
        }
        
        foreach (var port in watchedPorts)
        {
            _watched[port] = true;
        }
    }

    private readonly Dictionary<int, bool> _favorites = new();
    private readonly Dictionary<int, bool> _watched = new();

    public List<PortInfo> GetPorts()
    {
        lock (_lock)
        {
            return _ports.ToList();
        }
    }

    public async Task RefreshPortsAsync()
    {
        await Task.Run(() =>
        {
            var newPorts = new List<PortInfo>();
            var processedPorts = new HashSet<(int Port, string Protocol)>();
            
            // 获取当前端口列表用于比较（使用端口+协议作为复合键）
            var currentPorts = GetPorts().ToDictionary(p => (p.Port, p.Protocol));
            
            try
            {
                // 获取 TCP 端口
                var tcpListeners = IPGlobalProperties.GetIPGlobalProperties()
                    .GetActiveTcpListeners()
                    .Where(listener => listener.Port > 0)
                    .ToList();

                foreach (var listener in tcpListeners)
                {
                    var port = listener.Port;
                    var key = (port, "TCP");
                    
                    // 跳过已处理的端口，避免重复
                    if (processedPorts.Contains(key))
                    {
                        continue;
                    }
                    
                    var address = listener.Address?.ToString() ?? "127.0.0.1";
                    
                    var process = GetProcessForPort(port);
                    string processName = "Unknown";
                    int pid = 0;
                    string command = string.Empty;
                    
                    if (process != null)
                    {
                        processName = process.ProcessName;
                        pid = process.Id;
                        try
                        {
                            command = process.MainModule?.FileName ?? string.Empty;
                        }
                        catch
                        {
                            command = string.Empty;
                        }
                    }
                    
                    var portInfo = new PortInfo
                    {
                        Port = port,
                        ProcessName = processName,
                        Pid = pid,
                        Address = address,
                        User = Environment.UserName,
                        Command = command,
                        IsActive = true,
                        IsFavorite = _favorites.ContainsKey(port),
                        IsWatched = _watched.ContainsKey(port),
                        Protocol = "TCP"
                    };
                    
                    newPorts.Add(portInfo);
                    processedPorts.Add(key);
                }

                // 获取 UDP 端口
                var udpListeners = IPGlobalProperties.GetIPGlobalProperties()
                    .GetActiveUdpListeners()
                    .Where(listener => listener.Port > 0)
                    .ToList();

                foreach (var listener in udpListeners)
                {
                    var port = listener.Port;
                    var key = (port, "UDP");
                    
                    // 跳过已处理的端口，避免重复
                    if (processedPorts.Contains(key))
                    {
                        continue;
                    }
                    
                    var address = listener.Address?.ToString() ?? "127.0.0.1";
                    
                    var process = GetProcessForPort(port);
                    string processName = "Unknown";
                    int pid = 0;
                    string command = string.Empty;
                    
                    if (process != null)
                    {
                        processName = process.ProcessName;
                        pid = process.Id;
                        try
                        {
                            command = process.MainModule?.FileName ?? string.Empty;
                        }
                        catch
                        {
                            command = string.Empty;
                        }
                    }
                    
                    var portInfo = new PortInfo
                    {
                        Port = port,
                        ProcessName = processName,
                        Pid = pid,
                        Address = address,
                        User = Environment.UserName,
                        Command = command,
                        IsActive = true,
                        IsFavorite = _favorites.ContainsKey(port),
                        IsWatched = _watched.ContainsKey(port),
                        Protocol = "UDP"
                    };
                    
                    newPorts.Add(portInfo);
                    processedPorts.Add(key);
                    
                    // 检测新启动的端口（收藏或监控的端口）
                    if (!currentPorts.ContainsKey(key) && _watched.ContainsKey(port))
                    {
                        _notificationService.NotifyPortStarted(port, processName);
                    }
                }
                
                // 检测停止的端口（收藏或监控的端口）
                foreach (var oldPort in currentPorts.Values)
                {
                    if (!_watched.ContainsKey(oldPort.Port)) continue;
                    
                    var key = (oldPort.Port, oldPort.Protocol);
                    if (!processedPorts.Contains(key))
                    {
                        _notificationService.NotifyPortStopped(oldPort.Port);
                    }
                }
                
                // 原子性更新端口列表
                lock (_lock)
                {
                    _ports.Clear();
                    _ports.AddRange(newPorts);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning ports");
            }
        });
    }

    private Process? GetProcessForPort(int port)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                if (line.Contains($"127.0.0.1:{port}") || line.Contains($"0.0.0.0:{port}") || 
                    line.Contains($"[::]:{port}") || line.Contains($"[::1]:{port}"))
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5 && int.TryParse(parts[4], out int pid))
                    {
                        return Process.GetProcessById(pid);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting process for port {Port}", port);
        }

        return null;
    }

    public void KillPort(int port)
    {
        var portInfo = _ports.FirstOrDefault(p => p.Port == port);
        if (portInfo == null) 
        {
            _logger.LogWarning("Port {Port} not found in port list", port);
            return;
        }

        _logger.LogInformation("Attempting to kill port {Port}, PID: {Pid}, ProcessName: {ProcessName}", 
            port, portInfo.Pid, portInfo.ProcessName);

        // 检查是否是系统关键进程
        if (portInfo.Pid <= 4)
        {
            _logger.LogWarning("Cannot kill system process with PID {Pid}", portInfo.Pid);
            throw new UnauthorizedAccessException($"无法终止系统进程 {portInfo.ProcessName} (PID: {portInfo.Pid})。系统进程无法被终止。");
        }

        try
        {
            var process = Process.GetProcessById(portInfo.Pid);
            if (process != null)
            {
                _logger.LogInformation("Killing process {ProcessName} (PID: {Pid})", process.ProcessName, process.Id);
                process.Kill(entireProcessTree: true);
                
                // 等待进程退出
                var exited = process.WaitForExit(5000);
                
                if (exited)
                {
                    // 进程已退出，但检查是否是Windows服务（会自动重启）
                    if (IsWindowsService(process.ProcessName))
                    {
                        _logger.LogWarning("Process {ProcessName} is a Windows service and may auto-restart", process.ProcessName);
                        throw new UnauthorizedAccessException($"无法终止进程 {portInfo.ProcessName} (PID: {portInfo.Pid})。这是一个Windows系统服务，会自动重新启动。");
                    }
                    
                    _ports.Remove(portInfo);
                    _notificationService.NotifyPortKilled(port, portInfo.ProcessName);
                    _logger.LogInformation("Successfully killed process {ProcessName} (PID: {Pid})", process.ProcessName, process.Id);
                }
                else
                {
                    // 进程没有退出，可能是权限不足
                    _logger.LogWarning("Process {ProcessName} (PID: {Pid}) did not exit after Kill command", process.ProcessName, process.Id);
                    throw new UnauthorizedAccessException($"无法终止进程 {portInfo.ProcessName} (PID: {portInfo.Pid})。进程拒绝终止，可能需要管理员权限。");
                }
            }
            else
            {
                _logger.LogWarning("Process with PID {Pid} returned null", portInfo.Pid);
                _ports.Remove(portInfo);
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Process with PID {Pid} not found", portInfo.Pid);
            // 进程已经不存在，从列表中移除
            _ports.Remove(portInfo);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5) // ERROR_ACCESS_DENIED
        {
            _logger.LogWarning(ex, "Access denied when killing process {Pid}. Please run the application as Administrator to kill processes", portInfo.Pid);
            // 抛出异常让UI层处理，给用户友好的提示
            throw new UnauthorizedAccessException($"无法终止进程 {portInfo.ProcessName} (PID: {portInfo.Pid})。请以管理员身份运行应用程序。", ex);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogError(ex, "Win32 error when killing process {Pid}: {ErrorCode}", portInfo.Pid, ex.NativeErrorCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error killing process {Pid}", portInfo.Pid);
            throw;
        }
    }

    /// <summary>
    /// 检查进程是否是Windows系统服务
    /// </summary>
    private bool IsWindowsService(string processName)
    {
        // 常见的Windows系统服务进程列表
        var systemServices = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "svchost",      // Windows服务主机
            "services",     // 服务控制管理器
            "lsass",        // 本地安全认证子系统
            "csrss",        // 客户端/服务器运行时子系统
            "smss",         // 会话管理器子系统
            "wininit",      // Windows启动应用程序
            "winlogon",     // Windows登录应用程序
            "crss",         // 另一个系统进程
            "system",       // 系统进程
            "registry",     // 注册表
            "sshd",         // SSH服务
            "openssh",      // OpenSSH服务
            "TermService",  // 远程桌面服务
            "RpcSs",        // RPC服务
            "Dhcp",         // DHCP客户端
            "Dnscache",     // DNS客户端
            "NlaSvc",       // 网络位置感知
            "netlogon",     // Netlogon服务
            "LanmanServer", // 服务器服务
            "LanmanWorkstation", // 工作站服务
        };

        return systemServices.Contains(processName);
    }

    public void ToggleFavorite(int port)
    {
        var portInfo = _ports.FirstOrDefault(p => p.Port == port);
        if (portInfo == null) return;

        if (_favorites.ContainsKey(port))
        {
            _favorites.Remove(port);
            portInfo.IsFavorite = false;
        }
        else
        {
            _favorites[port] = true;
            portInfo.IsFavorite = true;
        }
        _settingsService.SaveFavorites(_favorites.Keys.ToHashSet());
    }

    public void ToggleWatch(int port)
    {
        var portInfo = _ports.FirstOrDefault(p => p.Port == port);
        if (portInfo == null) return;

        if (_watched.ContainsKey(port))
        {
            _watched.Remove(port);
            portInfo.IsWatched = false;
            
            var watchedPorts = _settingsService.GetWatchedPorts();
            watchedPorts.RemoveAll(w => w.Port == port);
            _settingsService.SaveWatchedPorts(watchedPorts);
        }
        else
        {
            _watched[port] = true;
            portInfo.IsWatched = true;
            
            var watchedPorts = _settingsService.GetWatchedPorts();
            watchedPorts.Add(new WatchedPort { Port = port });
            _settingsService.SaveWatchedPorts(watchedPorts);
        }
    }
}
