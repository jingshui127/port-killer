using NewLife.Log;
using PortKiller.Blazor.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace PortKiller.Blazor.Services;

public class PortScannerService
{
    private readonly List<PortInfo> _ports = new();
    private readonly object _lock = new();
    private readonly SettingsService _settingsService;
    private readonly NotificationService _notificationService;
    private readonly FirewallService _firewallService;

    public PortScannerService(SettingsService settingsService, NotificationService notificationService, FirewallService firewallService)
    {
        _settingsService = settingsService;
        _notificationService = notificationService;
        _firewallService = firewallService;
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
            
            // 一次性获取所有端口的进程信息（优化性能）
            var portProcessMap = GetAllPortProcesses();
            
            try
            {
                // 获取 TCP 监听端口
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
                    
                    // 从缓存的映射中获取进程信息
                    var (pid, processName, command, userName) = GetProcessInfoFromMap(portProcessMap, port, "TCP");
                    
                    var portInfo = new PortInfo
                    {
                        Port = port,
                        ProcessName = processName,
                        Pid = pid,
                        Address = address,
                        User = userName,
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
                    
                    // 从缓存的映射中获取进程信息
                    var (pid, processName, command, userName) = GetProcessInfoFromMap(portProcessMap, port, "UDP");
                    
                    var portInfo = new PortInfo
                    {
                        Port = port,
                        ProcessName = processName,
                        Pid = pid,
                        Address = address,
                        User = userName,
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
                
                // 原子性更新端口列表（先显示端口，不等待防火墙查询）
                lock (_lock)
                {
                    _ports.Clear();
                    _ports.AddRange(newPorts);
                }
                
                // 同步获取防火墙信息（暂时阻塞，确保数据完整）
                try
                {
                    var allPorts = newPorts.Select(p => p.Port).Distinct().ToList();
                    var firewallInfo = _firewallService.GetPortsFirewallInfo(allPorts);
                    
                    // 将防火墙信息应用到端口
                    foreach (var port in newPorts)
                    {
                        if (firewallInfo.TryGetValue(port.Port, out var info))
                        {
                            port.FirewallInfo = info;
                        }
                    }
                    
                    XTrace.Log.Debug($"Firewall info loaded for {firewallInfo.Count} ports");
                }
                catch (Exception ex)
                {
                    XTrace.Log.Debug($"Firewall info query failed (non-critical): {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                XTrace.Log.Error($"Error scanning ports: {ex.Message}");
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
            XTrace.Log.Error($"Error getting process for port {port}: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 一次性获取所有端口的进程信息（性能优化）
    /// </summary>
    private Dictionary<(int Port, string Protocol), (int Pid, string ProcessName, string Command, string UserName)> GetAllPortProcesses()
    {
        var result = new Dictionary<(int Port, string Protocol), (int Pid, string ProcessName, string Command, string UserName)>();
        
        try
        {
            // 只执行一次 netstat 获取所有信息
            var startInfo = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return result;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            
            // 收集所有需要查询的 PID
            var pidToPorts = new Dictionary<int, List<(int Port, string Protocol)>>();
            
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("TCP", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmedLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5)
                    {
                        var localAddress = parts[1];
                        var lastColonIndex = localAddress.LastIndexOf(':');
                        if (lastColonIndex > 0 && int.TryParse(localAddress.Substring(lastColonIndex + 1), out int port))
                        {
                            if (int.TryParse(parts[4], out int pid))
                            {
                                if (!pidToPorts.ContainsKey(pid))
                                    pidToPorts[pid] = new List<(int, string)>();
                                pidToPorts[pid].Add((port, "TCP"));
                            }
                        }
                    }
                }
                else if (trimmedLine.StartsWith("UDP", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmedLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        var localAddress = parts[1];
                        var lastColonIndex = localAddress.LastIndexOf(':');
                        if (lastColonIndex > 0 && int.TryParse(localAddress.Substring(lastColonIndex + 1), out int port))
                        {
                            if (int.TryParse(parts[3], out int pid))
                            {
                                if (!pidToPorts.ContainsKey(pid))
                                    pidToPorts[pid] = new List<(int, string)>();
                                pidToPorts[pid].Add((port, "UDP"));
                            }
                        }
                    }
                }
            }
            
            // 批量获取进程信息 - 使用 tasklist 命令一次获取所有进程
            var processInfoMap = GetAllProcessInfoBulk();
            
            // 填充结果
            foreach (var kvp in pidToPorts)
            {
                var pid = kvp.Key;
                var ports = kvp.Value;
                
                var processInfo = processInfoMap.GetValueOrDefault(pid, (ProcessName: "Unknown", Command: string.Empty, UserName: Environment.UserName));
                
                foreach (var (port, protocol) in ports)
                {
                    var key = (port, protocol);
                    if (!result.ContainsKey(key))
                    {
                        result[key] = (pid, processInfo.ProcessName, processInfo.Command, processInfo.UserName);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Error($"Error getting all port processes: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// 根据 PID 获取进程详细信息
    /// </summary>
    private (string ProcessName, string Command, string UserName) GetProcessDetailsByPid(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (process == null)
            {
                return ("Unknown", string.Empty, Environment.UserName);
            }

            string processName = "Unknown";
            string command = string.Empty;
            string userName = Environment.UserName;

            try
            {
                command = process.MainModule?.FileName ?? string.Empty;
                if (!string.IsNullOrEmpty(command))
                {
                    processName = Path.GetFileNameWithoutExtension(command);
                }
                else
                {
                    processName = process.ProcessName;
                }
            }
            catch
            {
                processName = process.ProcessName;
                command = string.Empty;
            }

            try
            {
                userName = GetProcessUserName(process);
            }
            catch
            {
                userName = Environment.UserName;
            }

            return (processName, command, userName);
        }
        catch
        {
            return ("Unknown", string.Empty, Environment.UserName);
        }
    }

    /// <summary>
    /// 从端口进程映射中获取进程信息
    /// </summary>
    private (int Pid, string ProcessName, string Command, string UserName) GetProcessInfoFromMap(
        Dictionary<(int Port, string Protocol), (int Pid, string ProcessName, string Command, string UserName)> map, 
        int port, 
        string protocol)
    {
        var key = (port, protocol);
        if (map.TryGetValue(key, out var info))
        {
            return info;
        }
        
        // 如果找不到，尝试查找任意协议的该端口
        if (map.TryGetValue((port, "TCP"), out var tcpInfo))
        {
            return tcpInfo;
        }
        if (map.TryGetValue((port, "UDP"), out var udpInfo))
        {
            return udpInfo;
        }
        
        return (0, "Unknown", string.Empty, Environment.UserName);
    }

    /// <summary>
    /// 使用 tasklist 命令批量获取所有进程信息（性能优化）
    /// </summary>
    private Dictionary<int, (string ProcessName, string Command, string UserName)> GetAllProcessInfoBulk()
    {
        var result = new Dictionary<int, (string ProcessName, string Command, string UserName)>();
        
        try
        {
            // 使用 tasklist 获取所有进程信息
            var startInfo = new ProcessStartInfo
            {
                FileName = "tasklist",
                Arguments = "/FO CSV /NH",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return result;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                // 解析 CSV 格式: "进程名","PID","会话名","会话#","内存使用"
                var parts = ParseCsvLine(line);
                if (parts.Length >= 2 && int.TryParse(parts[1], out int pid))
                {
                    var processName = parts[0].Trim('"');
                    // 去掉 .exe 后缀
                    if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        processName = processName.Substring(0, processName.Length - 4);
                    }
                    
                    result[pid] = (processName, string.Empty, Environment.UserName);
                }
            }
            
            // 尝试使用 WMI 获取进程路径信息（可选，如果慢可以注释掉）
            try
            {
                var wmiQuery = new System.Management.ManagementObjectSearcher(
                    "SELECT ProcessId, ExecutablePath FROM Win32_Process");
                
                foreach (System.Management.ManagementObject obj in wmiQuery.Get())
                {
                    var pid = Convert.ToInt32(obj["ProcessId"]);
                    var path = obj["ExecutablePath"]?.ToString() ?? string.Empty;
                    
                    if (result.ContainsKey(pid) && !string.IsNullOrEmpty(path))
                    {
                        var existing = result[pid];
                        result[pid] = (existing.ProcessName, path, existing.UserName);
                    }
                }
            }
            catch
            {
                // WMI 查询失败，使用已有信息
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Error($"Error getting process info bulk: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// 解析 CSV 行
    /// </summary>
    private string[] ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;
        
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }
        
        result.Add(current.ToString());
        return result.ToArray();
    }

    public void KillPort(int port)
    {
        var portInfo = _ports.FirstOrDefault(p => p.Port == port);
        if (portInfo == null) 
        {
            XTrace.Log.Warn($"Port {port} not found in port list");
            return;
        }

        XTrace.Log.Info($"Attempting to kill port {port}, PID: {portInfo.Pid}, ProcessName: {portInfo.ProcessName}");

        // 检查是否是系统关键进程
        if (portInfo.Pid <= 4)
        {
            XTrace.Log.Warn($"Cannot kill system process with PID {portInfo.Pid}");
            throw new UnauthorizedAccessException($"无法终止系统进程 {portInfo.ProcessName} (PID: {portInfo.Pid})。系统进程无法被终止。");
        }

        try
        {
            var process = Process.GetProcessById(portInfo.Pid);
            if (process != null)
            {
                XTrace.Log.Info($"Killing process {process.ProcessName} (PID: {process.Id})");
                process.Kill(entireProcessTree: true);
                
                // 等待进程退出
                var exited = process.WaitForExit(5000);
                
                if (exited)
                {
                    // 进程已退出，但检查是否是Windows服务（会自动重启）
                    if (IsWindowsService(process.ProcessName))
                    {
                        XTrace.Log.Warn($"Process {process.ProcessName} is a Windows service and may auto-restart");
                        throw new UnauthorizedAccessException($"无法终止进程 {portInfo.ProcessName} (PID: {portInfo.Pid})。这是一个Windows系统服务，会自动重新启动。");
                    }

                    _ports.Remove(portInfo);
                    _notificationService.NotifyPortKilled(port, portInfo.ProcessName);
                    XTrace.Log.Info($"Successfully killed process {process.ProcessName} (PID: {process.Id})");
                }
                else
                {
                    // 进程没有退出，可能是权限不足
                    XTrace.Log.Warn($"Process {process.ProcessName} (PID: {process.Id}) did not exit after Kill command");
                    throw new UnauthorizedAccessException($"无法终止进程 {portInfo.ProcessName} (PID: {portInfo.Pid})。进程拒绝终止，可能需要管理员权限。");
                }
            }
            else
            {
                XTrace.Log.Warn($"Process with PID {portInfo.Pid} returned null");
                _ports.Remove(portInfo);
            }
        }
        catch (ArgumentException ex)
        {
            XTrace.Log.Warn($"Process with PID {portInfo.Pid} not found: {ex.Message}");
            // 进程已经不存在，从列表中移除
            _ports.Remove(portInfo);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5) // ERROR_ACCESS_DENIED
        {
            XTrace.Log.Warn($"Access denied when killing process {portInfo.Pid}. Please run the application as Administrator to kill processes");
            // 抛出异常让UI层处理，给用户友好的提示
            throw new UnauthorizedAccessException($"无法终止进程 {portInfo.ProcessName} (PID: {portInfo.Pid})。请以管理员身份运行应用程序。", ex);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            XTrace.Log.Error($"Win32 error when killing process {portInfo.Pid}: {ex.NativeErrorCode}");
            throw;
        }
        catch (Exception ex)
        {
            XTrace.Log.Error($"Error killing process {portInfo.Pid}: {ex.Message}");
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

    /// <summary>
    /// 获取进程的用户名
    /// </summary>
    private string GetProcessUserName(Process process)
    {
        try
        {
            // 使用 WMI 查询获取进程的用户名
            var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT * FROM Win32_Process WHERE ProcessId = {process.Id}");
            
            foreach (System.Management.ManagementObject obj in searcher.Get())
            {
                string[] owner = new string[2];
                obj.InvokeMethod("GetOwner", owner);
                
                if (!string.IsNullOrEmpty(owner[0]))
                {
                    string domain = owner[1];
                    string user = owner[0];
                    
                    // 如果是系统账户，返回标准格式
                    if (string.IsNullOrEmpty(domain) || domain.Equals("NT AUTHORITY", StringComparison.OrdinalIgnoreCase))
                    {
                        return $"NT AUTHORITY\\{user}";
                    }
                    
                    return $"{domain}\\{user}";
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Debug($"Error getting process user name: {ex.Message}");
        }
        
        return Environment.UserName;
    }
}
