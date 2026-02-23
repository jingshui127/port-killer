using PortManager.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace PortManager.Services;

public class TunnelService
{
    private readonly ConcurrentDictionary<int, CloudflareTunnel> _tunnels = new();
    private readonly ConcurrentDictionary<int, Process> _tunnelProcesses = new();
    private readonly ConcurrentDictionary<int, string> _tunnelUrls = new();
    private readonly ConcurrentDictionary<int, string> _tunnelErrors = new();
    private readonly ConcurrentDictionary<int, bool> _processOutputAttached = new();
    private readonly ILogger<TunnelService>? _logger;
    private readonly SettingsService _settingsService;
    private readonly NotificationService _notificationService;
    private CloudflaredStatus? _cachedCloudflaredStatus;
    private DateTime _lastCloudflaredCheck = DateTime.MinValue;
    private bool _isInitialized = false;
    private readonly object _initLock = new();

    public event EventHandler<CloudflaredUpdateProgress>? UpdateProgressChanged;

    private static readonly string[] CloudflaredPaths = {
        @"C:\Program Files\cloudflared\cloudflared.exe",
        @"C:\Program Files (x86)\cloudflared\cloudflared.exe",
        @"C:\ProgramData\chocolatey\bin\cloudflared.exe",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"cloudflared\cloudflared.exe"),
        @"cloudflared.exe",
        @"/usr/local/bin/cloudflared",
        @"/usr/bin/cloudflared",
        @"/usr/local/cloudflared/cloudflared",
        @"cloudflared"
    };

    public static bool IsRunningAsAdministrator()
    {
#if WINDOWS
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
#else
        // Linux: 检查是否为 root 用户
        try
        {
            return Environment.UserName == "root" || 
                   Environment.GetEnvironmentVariable("USER") == "root" ||
                   File.Exists("/proc/1/status") && File.ReadAllText("/proc/1/status").Contains("Uid:\t0");
        }
        catch
        {
            return false;
        }
#endif
    }

    public TunnelService(SettingsService settingsService, ILogger<TunnelService>? logger = null, NotificationService? notificationService = null)
    {
        _logger = logger;
        _notificationService = notificationService ?? new NotificationService();
        _settingsService = settingsService;
    }

    public async Task InitializeAsync()
    {
        // 确保只初始化一次
        lock (_initLock)
        {
            if (_isInitialized)
            {
                _logger?.LogInformation("[TunnelService] Already initialized, skipping...");
                return;
            }
            _isInitialized = true;
        }
        
        var savedTunnels = _settingsService.GetActiveTunnels();
        
        foreach (var savedTunnel in savedTunnels)
        {
            var existingProcess = GetProcessForPort(savedTunnel.Port);
            
            if (existingProcess != null)
            {
                // 检查保存的URL是否有效，如果没有则尝试从字典获取
                var tunnelUrl = savedTunnel.TunnelUrl;
                if (string.IsNullOrEmpty(tunnelUrl) || tunnelUrl == "Unknown")
                {
                    tunnelUrl = _tunnelUrls.GetValueOrDefault(savedTunnel.Port, string.Empty);
                }
                
                var tunnel = new CloudflareTunnel
                {
                    Port = savedTunnel.Port,
                    Status = "Active",
                    TunnelUrl = tunnelUrl,
                    ProcessId = existingProcess.Id,
                    StartTime = existingProcess.StartTime,
                    TunnelName = savedTunnel.TunnelName,
                    Provider = savedTunnel.Provider,
                    Password = savedTunnel.Password
                };
                
                _tunnels[tunnel.Port] = tunnel;
                _tunnelProcesses[tunnel.Port] = existingProcess;
                _tunnelUrls[tunnel.Port] = tunnelUrl;
                
                // 为现有进程设置输出读取，以便捕获URL更新（只附加一次）
                if (!_processOutputAttached.ContainsKey(savedTunnel.Port))
                {
                    try
                    {
                        // 检查进程是否支持输出重定向
                        if (existingProcess.StartInfo.RedirectStandardOutput)
                        {
                            existingProcess.OutputDataReceived += (sender, e) =>
                            {
                                if (!string.IsNullOrEmpty(e.Data))
                                {
                                    ParseOutput(savedTunnel.Port, e.Data);
                                }
                            };
                            existingProcess.ErrorDataReceived += (sender, e) =>
                            {
                                if (!string.IsNullOrEmpty(e.Data))
                                {
                                    ParseOutput(savedTunnel.Port, e.Data);
                                }
                            };
                            existingProcess.BeginOutputReadLine();
                            existingProcess.BeginErrorReadLine();
                            _processOutputAttached[savedTunnel.Port] = true;
                        }
                        else
                        {
                            _logger?.LogDebug($"[TunnelService] Process for port {savedTunnel.Port} does not have redirected output");
                            _processOutputAttached[savedTunnel.Port] = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning($"[TunnelService] Could not attach to process output for port {savedTunnel.Port}: {ex.Message}");
                        _processOutputAttached[savedTunnel.Port] = false;
                    }
                }
            }
            else
            {
                // 没有找到对应的进程，保留隧道信息但标记为停止状态
                _logger?.LogInformation($"[TunnelService] No running process found for port {savedTunnel.Port}, keeping tunnel info for manual restart");
                
                var tunnel = new CloudflareTunnel
                {
                    Port = savedTunnel.Port,
                    Status = "Stopped",
                    TunnelUrl = savedTunnel.TunnelUrl,
                    ProcessId = 0,
                    StartTime = savedTunnel.StartTime,
                    TunnelName = savedTunnel.TunnelName,
                    Provider = savedTunnel.Provider,
                    Password = savedTunnel.Password
                };
                
                _tunnels[tunnel.Port] = tunnel;
                _tunnelUrls[tunnel.Port] = savedTunnel.TunnelUrl;
            }
        }
        
        SaveActiveTunnels();
        
        _ = CleanupOrphanedTunnelsAsync();
    }

    private async Task RestartTunnelOnStartupAsync(CloudflareTunnel savedTunnel)
    {
        try
        {
            _logger?.LogInformation($"[TunnelService] Auto-restarting tunnel for port {savedTunnel.Port}");
            
            // 保留原来的URL，如果重启失败可以恢复
            var originalUrl = savedTunnel.TunnelUrl;
            
            var newTunnel = new CloudflareTunnel
            {
                Port = savedTunnel.Port,
                Status = "Starting",
                StartTime = DateTime.Now,
                TunnelName = savedTunnel.TunnelName ?? $"port-{savedTunnel.Port}-tunnel",
                TunnelUrl = originalUrl, // 保留原URL
                Provider = savedTunnel.Provider // 保留原提供商类型
            };

            _tunnels[savedTunnel.Port] = newTunnel;
            _tunnelUrls[savedTunnel.Port] = originalUrl; // 同时更新URL字典
            SaveActiveTunnels();

            // 根据提供商类型选择启动方法
            Process tunnelProcess;
            if (savedTunnel.Provider == TunnelProvider.LocalTunnel)
            {
                tunnelProcess = await StartLocalTunnelAsync(savedTunnel.Port, newTunnel.TunnelName);
                // LocalTunnel 启动时获取密码
                newTunnel.Password = await GetLocalTunnelPasswordAsync();
            }
            else
            {
                tunnelProcess = await StartCloudflaredAsync(savedTunnel.Port, newTunnel.TunnelName);
            }
            
            newTunnel.ProcessId = tunnelProcess.Id;
            newTunnel.Status = "Active";
            
            _tunnelErrors[savedTunnel.Port] = string.Empty;
            
            var timeout = TimeSpan.FromSeconds(30);
            var startTime = DateTime.Now;

            while (DateTime.Now - startTime < timeout)
            {
                if (!string.IsNullOrEmpty(_tunnelUrls[savedTunnel.Port]) && _tunnelUrls[savedTunnel.Port] != originalUrl)
                {
                    // 获取到新URL，更新隧道信息
                    newTunnel.TunnelUrl = _tunnelUrls[savedTunnel.Port];
                    SaveActiveTunnels();
                    _logger?.LogInformation($"[TunnelService] Tunnel auto-restarted successfully: {newTunnel.TunnelUrl}");
                    return;
                }
                
                if (!string.IsNullOrEmpty(_tunnelErrors[savedTunnel.Port]))
                {
                    _logger?.LogError($"[TunnelService] Failed to auto-restart tunnel: {_tunnelErrors[savedTunnel.Port]}");
                    newTunnel.Status = "Error";
                    newTunnel.LastError = _tunnelErrors[savedTunnel.Port];
                    // 保留原URL
                    newTunnel.TunnelUrl = originalUrl;
                    _tunnelUrls[savedTunnel.Port] = originalUrl;
                    SaveActiveTunnels();
                    return;
                }
                
                if (tunnelProcess.HasExited)
                {
                    _logger?.LogError($"[TunnelService] Tunnel process exited with code {tunnelProcess.ExitCode}");
                    newTunnel.Status = "Error";
                    newTunnel.LastError = $"Process exited with code {tunnelProcess.ExitCode}";
                    // 保留原URL
                    newTunnel.TunnelUrl = originalUrl;
                    _tunnelUrls[savedTunnel.Port] = originalUrl;
                    SaveActiveTunnels();
                    return;
                }
                
                await Task.Delay(500);
            }
            
            _logger?.LogError($"[TunnelService] Timeout waiting for tunnel URL");
            newTunnel.Status = "Error";
            newTunnel.LastError = "Timeout waiting for tunnel URL";
            // 保留原URL
            newTunnel.TunnelUrl = originalUrl;
            _tunnelUrls[savedTunnel.Port] = originalUrl;
            SaveActiveTunnels();
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[TunnelService] Error auto-restarting tunnel: {ex.Message}");
            if (_tunnels.TryGetValue(savedTunnel.Port, out var tunnel))
            {
                tunnel.Status = "Error";
                tunnel.LastError = ex.Message;
                // 保留原URL
                tunnel.TunnelUrl = savedTunnel.TunnelUrl;
                _tunnelUrls[savedTunnel.Port] = savedTunnel.TunnelUrl;
                SaveActiveTunnels();
            }
        }
    }

    public List<CloudflareTunnel> GetTunnels()
    {
        var tunnels = _tunnels.Values.ToList();

        // 同步_tunnelUrls字典中的URL到隧道对象
        foreach (var tunnel in tunnels)
        {
            if (_tunnelUrls.TryGetValue(tunnel.Port, out var url) && !string.IsNullOrEmpty(url) && url != "Unknown")
            {
                tunnel.TunnelUrl = url;
            }
            // 如果隧道对象有有效URL但字典中没有，同步到字典
            else if (!string.IsNullOrEmpty(tunnel.TunnelUrl) && tunnel.TunnelUrl != "Unknown")
            {
                _tunnelUrls[tunnel.Port] = tunnel.TunnelUrl;
            }
        }

        return tunnels;
    }

    public bool HasTunnelForPort(int port)
    {
        return _tunnels.ContainsKey(port);
    }

    public CloudflareTunnel? GetTunnelForPort(int port)
    {
        if (_tunnels.TryGetValue(port, out var tunnel))
        {
            // 同步URL
            if (_tunnelUrls.TryGetValue(port, out var url) && !string.IsNullOrEmpty(url))
            {
                tunnel.TunnelUrl = url;
            }
            return tunnel;
        }
        return null;
    }

    public Process? GetProcessForPort(int port)
    {
        if (OperatingSystem.IsWindows())
        {
            return GetProcessForPortWindows(port);
        }
        else if (OperatingSystem.IsLinux())
        {
            return GetProcessForPortLinux(port);
        }
        
        return null;
    }

    private Process? GetProcessForPortWindows(int port)
    {
        // 首先检查已知的进程
        foreach (var kvp in _tunnelProcesses)
        {
            var process = kvp.Value;
            if (!process.HasExited)
            {
                var commandLine = GetProcessCommandLine(process.Id);
                if (commandLine != null && 
                    (commandLine.Contains($"--url localhost:{port}") || 
                     commandLine.Contains($"--port {port}")))
                {
                    return process;
                }
            }
        }
        
        // 查找 Cloudflare 进程
        var foundProcess = FindProcessByCommandLine("cloudflared", $"--url localhost:{port}");
        if (foundProcess != null) return foundProcess;
        
        // 查找 LocalTunnel 进程 (lt.exe)
        foundProcess = FindProcessByCommandLine("lt", $"--port {port}");
        if (foundProcess != null) return foundProcess;
        
        // 查找 npx 启动的 LocalTunnel
        foundProcess = FindProcessByCommandLine("node", $"localtunnel --port {port}");
        if (foundProcess != null) return foundProcess;
        
        return null;
    }
    
    private Process? FindProcessByCommandLine(string processName, string commandLinePattern)
    {
        var processes = Process.GetProcessesByName(processName);
        Process? foundProcess = null;
        var processesToDispose = new List<Process>();
        
        foreach (var process in processes)
        {
            try
            {
                var commandLine = GetProcessCommandLine(process.Id);
                if (commandLine != null && commandLine.Contains(commandLinePattern))
                {
                    foundProcess = process;
                }
                else
                {
                    processesToDispose.Add(process);
                }
            }
            catch
            {
                processesToDispose.Add(process);
            }
        }
        
        foreach (var p in processesToDispose)
        {
            try
            {
                p.Dispose();
            }
            catch
            {
            }
        }
        
        return foundProcess;
    }

    private Process? GetProcessForPortLinux(int port)
    {
        // 首先检查已知的进程
        foreach (var kvp in _tunnelProcesses)
        {
            var process = kvp.Value;
            if (!process.HasExited)
            {
                return process;
            }
        }
        
        // 查找 Cloudflare 进程
        var foundProcess = FindProcessByCommandLine("cloudflared", $"--url localhost:{port}");
        if (foundProcess != null) return foundProcess;
        
        // 查找 LocalTunnel 进程 (lt)
        foundProcess = FindProcessByCommandLine("lt", $"--port {port}");
        if (foundProcess != null) return foundProcess;
        
        // 查找 npx/node 启动的 LocalTunnel
        foundProcess = FindProcessByCommandLine("node", $"localtunnel --port {port}");
        if (foundProcess != null) return foundProcess;
        
        // 查找 npx 进程
        foundProcess = FindProcessByCommandLine("npx", $"localtunnel --port {port}");
        if (foundProcess != null) return foundProcess;
        
        return null;
    }
    
    public string? GetProcessCommandLine(int processId)
    {
#if WINDOWS
        return GetProcessCommandLineWindows(processId);
#else
        return GetProcessCommandLineLinux(processId);
#endif
    }

#if WINDOWS
    private string? GetProcessCommandLineWindows(int processId)
    {
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {processId}");
            
            foreach (System.Management.ManagementObject obj in searcher.Get())
            {
                return obj["CommandLine"]?.ToString();
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }
#endif

    private string? GetProcessCommandLineLinux(int processId)
    {
        try
        {
            var cmdlinePath = $"/proc/{processId}/cmdline";
            if (!File.Exists(cmdlinePath))
            {
                return null;
            }
            
            var cmdline = File.ReadAllText(cmdlinePath);
            
            if (string.IsNullOrEmpty(cmdline))
            {
                return null;
            }
            
            var args = cmdline.Split('\0');
            return string.Join(" ", args.Where(a => !string.IsNullOrEmpty(a)));
        }
        catch
        {
            return null;
        }
    }

    public CloudflareTunnel? GetTunnel(int port)
    {
        return _tunnels.GetValueOrDefault(port);
    }

    public async Task<CloudflareTunnel> CreateTunnelAsync(int port, string? tunnelName = null, TunnelProvider provider = TunnelProvider.Cloudflare)
    {
        if (_tunnels.ContainsKey(port))
        {
            throw new InvalidOperationException($"Tunnel for port {port} already exists");
        }

        // Generate a stable tunnel name if not provided
        var stableTunnelName = tunnelName ?? $"port-{port}-tunnel";

        var tunnel = new CloudflareTunnel
        {
            Port = port,
            Status = "Starting",
            StartTime = DateTime.Now,
            TunnelName = stableTunnelName,
            TunnelUrl = string.Empty,
            Provider = provider,
            Password = provider == TunnelProvider.LocalTunnel ? await GetLocalTunnelPasswordAsync() : null
        };

        _tunnels[port] = tunnel;
        SaveActiveTunnels();

        try
        {
            // 先清空之前的 URL 和错误信息
            _tunnelUrls[port] = string.Empty;
            _tunnelErrors[port] = string.Empty;
            
            Process tunnelProcess;
            if (provider == TunnelProvider.LocalTunnel)
            {
                tunnelProcess = await StartLocalTunnelAsync(port, stableTunnelName);
            }
            else
            {
                tunnelProcess = await StartCloudflaredAsync(port, stableTunnelName);
            }
            tunnel.ProcessId = tunnelProcess.Id;
            tunnel.Status = "Active";
            
            var timeout = TimeSpan.FromSeconds(30);
            var startTime = DateTime.Now;

            while (DateTime.Now - startTime < timeout)
            {
                if (!string.IsNullOrEmpty(_tunnelUrls[port]))
                {
                    tunnel.TunnelUrl = _tunnelUrls[port];
                    SaveActiveTunnels();
                    _logger?.LogInformation($"[TunnelService] Tunnel created successfully: {tunnel.TunnelUrl}");
                    return tunnel;
                }
                
                if (!string.IsNullOrEmpty(_tunnelErrors[port]))
                {
                    var errorMsg = _tunnelErrors[port];
                    // 检测特定的 Cloudflare 服务错误
                    if (errorMsg.Contains("Worker threw exception", StringComparison.OrdinalIgnoreCase) ||
                        errorMsg.Contains("unmarshaling", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("Cloudflare 临时隧道服务暂时不可用，请稍后重试。错误：" + errorMsg);
                    }
                    // 检测 LocalTunnel 错误
                    if (provider == TunnelProvider.LocalTunnel)
                    {
                        throw new InvalidOperationException($"LocalTunnel error: {errorMsg}");
                    }
                    throw new InvalidOperationException($"Tunnel error: {errorMsg}");
                }
                
                if (tunnelProcess.HasExited)
                {
                    throw new InvalidOperationException($"Tunnel process exited with code {tunnelProcess.ExitCode}");
                }
                
                await Task.Delay(500);
            }
            
            throw new TimeoutException("Timeout waiting for tunnel URL");
        }
        catch (Exception ex)
        {
            tunnel.Status = "Failed";
            tunnel.LastError = ex.Message;
            _tunnels.TryRemove(port, out _);
            SaveActiveTunnels();
            throw;
        }
    }

    private async Task<Process> StartCloudflaredAsync(int port, string? tunnelName)
    {
        var cloudflaredPath = GetCloudflaredPath();
        if (cloudflaredPath == null)
        {
            throw new InvalidOperationException("cloudflared is not installed");
        }

        var arguments = $"--url localhost:{port}";
        _logger?.LogInformation($"[TunnelService] Starting cloudflared with arguments: {arguments}");
        _logger?.LogInformation($"[TunnelService] Cloudflared path: {cloudflaredPath}");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = cloudflaredPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(cloudflaredPath)
            }
        };

        process.OutputDataReceived += (sender, e) => 
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logger?.LogInformation($"[TunnelService] Output: {e.Data}");
                ParseOutput(port, e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) => 
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                var lowerLine = e.Data.ToLower();
                if (lowerLine.Contains("err") || 
                    lowerLine.Contains("fatal") ||
                    lowerLine.Contains("failed to dial") ||
                    lowerLine.Contains("permission denied") ||
                    lowerLine.Contains("could not") ||
                    lowerLine.Contains("unable to"))
                {
                    _logger?.LogError($"[TunnelService] Error: {e.Data}");
                }
                else
                {
                    _logger?.LogInformation($"[TunnelService] Cloudflared: {e.Data}");
                }
                ParseOutput(port, e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            _tunnelProcesses[port] = process;

            await Task.Delay(1000);

            if (process.HasExited)
            {
                throw new InvalidOperationException($"Cloudflared process exited with code {process.ExitCode}");
            }

            _logger?.LogInformation($"[TunnelService] Cloudflared process started with ID: {process.Id}");
            return process;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[TunnelService] Failed to start tunnel: {ex.Message}");
            throw;
        }
    }

    public void StopTunnel(int port)
    {
        // 如果隧道不存在，直接返回（可能已经被删除）
        if (!_tunnels.ContainsKey(port))
        {
            _logger?.LogWarning($"[TunnelService] No tunnel found for port {port}, may have been already removed");
            return;
        }

        // 首先尝试从字典中移除并终止进程
        if (_tunnelProcesses.TryRemove(port, out var process))
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error stopping tunnel process: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }

        // 额外检查：终止所有该端口的 cloudflared 进程（处理孤儿进程）
        try
        {
            var allCloudflaredProcesses = Process.GetProcessesByName("cloudflared");
            foreach (var cloudflaredProcess in allCloudflaredProcesses)
            {
                try
                {
                    if (OperatingSystem.IsWindows())
                    {
                        var commandLine = GetProcessCommandLine(cloudflaredProcess.Id);
                        if (commandLine != null && commandLine.Contains($"--url localhost:{port}"))
                        {
                            if (!cloudflaredProcess.HasExited)
                            {
                                _logger?.LogInformation($"[TunnelService] Killing orphaned cloudflared process for port {port} (PID: {cloudflaredProcess.Id})");
                                cloudflaredProcess.Kill(entireProcessTree: true);
                                cloudflaredProcess.WaitForExit(3000);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"[TunnelService] Error checking/killing cloudflared process: {ex.Message}");
                }
                finally
                {
                    cloudflaredProcess.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[TunnelService] Error cleaning up cloudflared processes: {ex.Message}");
        }

        _tunnelUrls.TryRemove(port, out _);
        _tunnelErrors.TryRemove(port, out _);
        _processOutputAttached.TryRemove(port, out _);

        _tunnels.TryRemove(port, out _);
        SaveActiveTunnels();
        
        _notificationService?.NotifyTunnelStopped(port);
    }

    public void StopAllTunnels()
    {
        var ports = _tunnels.Keys.ToList();
        foreach (var port in ports)
        {
            try
            {
                StopTunnel(port);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error stopping tunnel on port {port}: {ex.Message}");
            }
        }
    }

    public void SaveActiveTunnels()
    {
        var activeTunnels = _tunnels.Values.ToList();
        
        // 调试日志：记录保存的隧道信息
        foreach (var tunnel in activeTunnels)
        {
            _logger?.LogInformation($"[TunnelService] Saving tunnel - Port: {tunnel.Port}, URL: {tunnel.TunnelUrl}, Status: {tunnel.Status}");
        }
        
        _settingsService.SaveActiveTunnels(activeTunnels);
        _logger?.LogInformation($"[TunnelService] Saved {activeTunnels.Count} tunnels to settings");
    }

    public async Task<CloudflareTunnel> RestartTunnelAsync(int port)
    {
        _logger?.LogInformation($"[TunnelService] Restarting tunnel for port {port}");

        if (!_tunnels.ContainsKey(port))
        {
            throw new InvalidOperationException($"No tunnel found for port {port}");
        }

        var tunnel = _tunnels[port];
        tunnel.Status = "Stopping";

        try
        {
            if (_tunnelProcesses.TryRemove(port, out var process))
            {
                try
                {
                    _logger?.LogInformation($"[TunnelService] Killing process {process.Id} for port {port}");
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"[TunnelService] Error stopping tunnel process: {ex.Message}");
                }
            }

            _tunnelUrls.TryRemove(port, out _);
            _tunnelErrors.TryRemove(port, out _);
            _tunnels.TryRemove(port, out _);
            SaveActiveTunnels();

            _logger?.LogInformation($"[TunnelService] Tunnel for port {port} stopped, waiting 2 seconds...");
            await Task.Delay(2000);

            _logger?.LogInformation($"[TunnelService] Creating new tunnel for port {port}");
            
            var newTunnel = new CloudflareTunnel
            {
                Port = port,
                Status = "Starting",
                StartTime = DateTime.Now,
                TunnelName = $"port-{port}-tunnel",
                TunnelUrl = string.Empty
            };

            _tunnels[port] = newTunnel;
            SaveActiveTunnels();

            var cloudflaredProcess = await StartCloudflaredAsync(port, newTunnel.TunnelName);
            newTunnel.ProcessId = cloudflaredProcess.Id;
            newTunnel.Status = "Active";
            
            _tunnelUrls[port] = string.Empty;
            _tunnelErrors[port] = string.Empty;
            
            var timeout = TimeSpan.FromSeconds(30);
            var startTime = DateTime.Now;

            while (DateTime.Now - startTime < timeout)
            {
                if (!string.IsNullOrEmpty(_tunnelUrls[port]))
                {
                    newTunnel.TunnelUrl = _tunnelUrls[port];
                    SaveActiveTunnels();
                    _logger?.LogInformation($"[TunnelService] Tunnel created successfully: {newTunnel.TunnelUrl}");
                    _notificationService?.NotifyTunnelRestarted(port);
                    return newTunnel;
                }
                
                if (!string.IsNullOrEmpty(_tunnelErrors[port]))
                {
                    throw new InvalidOperationException($"Cloudflared error: {_tunnelErrors[port]}");
                }
                
                if (cloudflaredProcess.HasExited)
                {
                    throw new InvalidOperationException($"Cloudflared process exited with code {cloudflaredProcess.ExitCode}");
                }
                
                await Task.Delay(500);
            }
            
            throw new TimeoutException("Timeout waiting for tunnel URL");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[TunnelService] Error restarting tunnel: {ex.Message}");
            if (_tunnels.TryGetValue(port, out var currentTunnel))
            {
                currentTunnel.Status = "Error";
                currentTunnel.LastError = ex.Message;
            }
            throw;
        }
    }

    public void UpdateTunnelStatus()
    {
        foreach (var tunnel in _tunnels.Values.ToList())
        {
            if (_tunnelProcesses.TryGetValue(tunnel.Port, out var process))
            {
                try
                {
                    if (process.HasExited)
                    {
                        tunnel.Status = "Stopped";
                        tunnel.LastError = "Process exited unexpectedly";
                    }
                    else
                    {
                        var uptime = DateTime.Now - tunnel.StartTime;
                        tunnel.Uptime = FormatUptime(uptime);
                    }
                }
                catch (InvalidOperationException)
                {
                    tunnel.Status = "Stopped";
                    tunnel.LastError = "Process no longer available";
                }
            }
        }
    }

    public async Task<List<CloudflareTunnel>> ScanTunnelsAsync()
    {
        var scannedTunnels = new List<CloudflareTunnel>();
        
        // 获取保存的隧道信息用于恢复URL
        var savedTunnels = _settingsService.GetActiveTunnels();
        var savedTunnelsDict = savedTunnels.ToDictionary(t => t.Port, t => t);
        
        await Task.Run(() =>
        {
            try
            {
                var processes = Process.GetProcessesByName("cloudflared");
                var processesToDispose = new List<Process>();
                
                foreach (var process in processes)
                {
                    bool shouldDispose = true;
                    try
                    {
                        if (OperatingSystem.IsWindows())
                        {
                            var commandLine = GetProcessCommandLine(process.Id);
                            if (commandLine != null && commandLine.Contains("--url"))
                            {
                                var match = Regex.Match(commandLine, @"--url\s+localhost:(\d+)");
                                if (match.Success && int.TryParse(match.Groups[1].Value, out int port))
                                {
                                    var existingTunnel = _tunnels.Values.FirstOrDefault(t => t.Port == port);
                                    
                                    // 尝试从保存的设置中恢复URL（优先使用有效的URL）
                                    string tunnelUrl = string.Empty;
                                    string tunnelName = $"Tunnel-{port}";
                                    if (savedTunnelsDict.TryGetValue(port, out var savedTunnel))
                                    {
                                        tunnelName = savedTunnel.TunnelName ?? tunnelName;
                                        // 只使用有效的URL
                                        if (!string.IsNullOrEmpty(savedTunnel.TunnelUrl) && savedTunnel.TunnelUrl != "Unknown")
                                        {
                                            tunnelUrl = savedTunnel.TunnelUrl;
                                        }
                                    }
                                    
                                    // 如果保存的URL无效，尝试从内存字典获取
                                    if (string.IsNullOrEmpty(tunnelUrl) && _tunnelUrls.ContainsKey(port))
                                    {
                                        tunnelUrl = _tunnelUrls[port];
                                    }
                                    
                                    if (existingTunnel == null)
                                    {
                                        var startTime = DateTime.Now;
                                        try
                                        {
                                            startTime = process.StartTime;
                                        }
                                        catch
                                        {
                                        }
                                        
                                        var newTunnel = new CloudflareTunnel
                                        {
                                            Port = port,
                                            TunnelName = tunnelName,
                                            Status = process.HasExited ? "Stopped" : "Active",
                                            ProcessId = process.Id,
                                            StartTime = startTime,
                                            Uptime = FormatUptime(DateTime.Now - startTime),
                                            TunnelUrl = tunnelUrl
                                        };
                                        
                                        _tunnels[port] = newTunnel;
                                        _tunnelProcesses[port] = process;
                                        _tunnelUrls[port] = tunnelUrl;
                                        scannedTunnels.Add(newTunnel);
                                        shouldDispose = false;
                                        
                                        // 为现有进程开始读取输出以捕获URL（只附加一次）
                                        if (!_processOutputAttached.ContainsKey(port))
                                        {
                                            try
                                            {
                                                if (process.StartInfo.RedirectStandardOutput)
                                                {
                                                    process.OutputDataReceived += (sender, e) =>
                                                    {
                                                        if (!string.IsNullOrEmpty(e.Data))
                                                        {
                                                            ParseOutput(port, e.Data);
                                                        }
                                                    };
                                                    process.ErrorDataReceived += (sender, e) =>
                                                    {
                                                        if (!string.IsNullOrEmpty(e.Data))
                                                        {
                                                            ParseOutput(port, e.Data);
                                                        }
                                                    };
                                                    process.BeginOutputReadLine();
                                                    process.BeginErrorReadLine();
                                                    _processOutputAttached[port] = true;
                                                }
                                                else
                                                {
                                                    _processOutputAttached[port] = false;
                                                }
                                            }
                                            catch
                                            {
                                                _processOutputAttached[port] = false;
                                            }
                                        }
                                        
                                        SaveActiveTunnels();
                                    }
                                    else
                                    {
                                        existingTunnel.ProcessId = process.Id;
                                        existingTunnel.Status = process.HasExited ? "Stopped" : "Active";
                                        existingTunnel.Uptime = FormatUptime(DateTime.Now - existingTunnel.StartTime);
                                        // 恢复保存的URL（如果当前URL为空、Unknown，且新URL有效）
                                        if ((string.IsNullOrEmpty(existingTunnel.TunnelUrl) || existingTunnel.TunnelUrl == "Unknown") 
                                            && !string.IsNullOrEmpty(tunnelUrl) && tunnelUrl != "Unknown")
                                        {
                                            existingTunnel.TunnelUrl = tunnelUrl;
                                            _tunnelUrls[port] = tunnelUrl;
                                        }
                                        shouldDispose = false;
                                        
                                        // 为现有进程开始读取输出以捕获URL（只附加一次）
                                        if (!_processOutputAttached.ContainsKey(port))
                                        {
                                            try
                                            {
                                                if (process.StartInfo.RedirectStandardOutput)
                                                {
                                                    process.OutputDataReceived += (sender, e) =>
                                                    {
                                                        if (!string.IsNullOrEmpty(e.Data))
                                                        {
                                                            ParseOutput(port, e.Data);
                                                        }
                                                    };
                                                    process.ErrorDataReceived += (sender, e) =>
                                                    {
                                                        if (!string.IsNullOrEmpty(e.Data))
                                                        {
                                                            ParseOutput(port, e.Data);
                                                        }
                                                    };
                                                    process.BeginOutputReadLine();
                                                    process.BeginErrorReadLine();
                                                    _processOutputAttached[port] = true;
                                                }
                                                else
                                                {
                                                    _processOutputAttached[port] = false;
                                                }
                                            }
                                            catch
                                            {
                                                _processOutputAttached[port] = false;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        if (shouldDispose)
                        {
                            processesToDispose.Add(process);
                        }
                    }
                }
                
                foreach (var p in processesToDispose)
                {
                    try
                    {
                        p.Dispose();
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error scanning tunnels: {ex.Message}");
            }
        });
        
        return scannedTunnels;
    }

    private string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        }
        else if (uptime.TotalHours >= 1)
        {
            return $"{uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
        }
        else
        {
            return $"{uptime.Minutes}m {uptime.Seconds}s";
        }
    }

    public bool IsCloudflaredInstalled()
    {
        try
        {
            var fileName = OperatingSystem.IsWindows() ? "cloudflared.exe" : "cloudflared";
            
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public string? GetCloudflaredPath()
    {
        var explicitPath = CloudflaredPaths.Take(CloudflaredPaths.Length - 1)
            .FirstOrDefault(File.Exists);
        
        if (explicitPath != null)
            return explicitPath;

        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "cloudflared",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var firstPath = output.Split('\n')[0].Trim();
                if (File.Exists(firstPath))
                    return firstPath;
            }
        }
        catch
        {
        }

        return null;
    }

    public string? GetCloudflaredVersion()
    {
        try
        {
            var fileName = OperatingSystem.IsWindows() ? "cloudflared.exe" : "cloudflared";
            
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            return output.Trim();
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetLatestCloudflaredVersionAsync()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "PortManager");
            
            var response = await httpClient.GetStringAsync("https://api.github.com/repos/cloudflare/cloudflared/releases/latest");
            
            var versionMatch = Regex.Match(response, @"""tag_name"":\s*""([^""]+)""");
            if (versionMatch.Success)
            {
                return versionMatch.Groups[1].Value;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error checking latest cloudflared version: {ex.Message}");
            return null;
        }
    }

    public async Task<CloudflaredStatus> GetCloudflaredStatusWithUpdateCheckAsync()
    {
        var now = DateTime.Now;
        
        if (_cachedCloudflaredStatus != null && (now - _lastCloudflaredCheck).TotalMinutes < 5)
        {
            return _cachedCloudflaredStatus;
        }
        
        var isInstalled = IsCloudflaredInstalled();
        var currentVersion = GetCloudflaredVersion();
        var latestVersion = await GetLatestCloudflaredVersionAsync();
        
        var hasUpdate = false;
        if (!string.IsNullOrEmpty(currentVersion) && !string.IsNullOrEmpty(latestVersion))
        {
            var currentVer = ParseVersion(currentVersion);
            var latestVer = ParseVersion(latestVersion);
            hasUpdate = latestVer > currentVer;
        }

        _cachedCloudflaredStatus = new CloudflaredStatus
        {
            IsInstalled = isInstalled,
            Version = currentVersion,
            LatestVersion = latestVersion,
            HasUpdate = hasUpdate
        };
        
        _lastCloudflaredCheck = now;
        
        return _cachedCloudflaredStatus;
    }

    private Version ParseVersion(string versionString)
    {
        var match = Regex.Match(versionString, @"(\d+)\.(\d+)\.(\d+)");
        if (match.Success)
        {
            return new Version(
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value)
            );
        }
        return new Version(0, 0, 0);
    }

    public async Task<string?> UpdateCloudflaredAsync()
    {
        void ReportProgress(string status, string message, int progress = 0, bool isComplete = false, bool isError = false, string? version = null)
        {
            _logger?.LogInformation($"[TunnelService] Progress: {status} - {message} ({progress}%)");
            UpdateProgressChanged?.Invoke(this, new CloudflaredUpdateProgress
            {
                Status = status,
                Message = message,
                Progress = progress,
                IsComplete = isComplete,
                IsError = isError,
                Version = version
            });
        }

        try
        {
            var cloudflaredPath = GetCloudflaredPath();
            if (cloudflaredPath == null)
            {
                ReportProgress("error", "cloudflared 未安装", 0, true, true);
                throw new InvalidOperationException("cloudflared is not installed");
            }

            ReportProgress("checking", "检查更新...", 5);
            _logger?.LogInformation($"[TunnelService] Starting cloudflared update from: {cloudflaredPath}");
            _logger?.LogInformation($"[TunnelService] Cloudflared path exists: {File.Exists(cloudflaredPath)}");

            ReportProgress("downloading", "正在下载最新版本...", 10);

            var startInfo = new ProcessStartInfo
            {
                FileName = cloudflaredPath,
                Arguments = "update",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(cloudflaredPath) ?? Environment.CurrentDirectory
            };

            _logger?.LogInformation($"[TunnelService] Running: {cloudflaredPath} update");

            using var process = Process.Start(startInfo);
            if (process == null) 
            {
                ReportProgress("error", "无法启动升级进程", 0, true, true);
                throw new InvalidOperationException("Failed to start cloudflared update process");
            }

            var outputBuilder = new System.Text.StringBuilder();
            var errorBuilder = new System.Text.StringBuilder();
            var progress = 20;

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                    _logger?.LogInformation($"[Cloudflared Update stdout] {e.Data}");
                    
                    var line = e.Data.ToLower();
                    if (line.Contains("downloading") || line.Contains("fetching"))
                    {
                        progress = Math.Min(progress + 5, 60);
                        ReportProgress("downloading", "正在下载最新版本...", progress);
                    }
                    else if (line.Contains("verifying") || line.Contains("checksum"))
                    {
                        progress = Math.Min(progress + 5, 70);
                        ReportProgress("verifying", "正在验证下载文件...", progress);
                    }
                    else if (line.Contains("installing") || line.Contains("extracting") || line.Contains("replacing"))
                    {
                        progress = Math.Min(progress + 5, 85);
                        ReportProgress("installing", "正在安装新版本...", progress);
                    }
                    else if (line.Contains("success") || line.Contains("complete"))
                    {
                        progress = 90;
                        ReportProgress("installing", "安装完成，正在验证...", progress);
                    }
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                    _logger?.LogError($"[Cloudflared Update stderr] {e.Data}");
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var timeoutMs = 120000;
            using var cts = new System.Threading.CancellationTokenSource(timeoutMs);
            
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(); } catch { }
                ReportProgress("error", $"升级超时（{timeoutMs/1000}秒）", 0, true, true);
                throw new InvalidOperationException($"升级超时（{timeoutMs/1000}秒），请手动下载最新版本：https://github.com/cloudflare/cloudflared/releases");
            }

            var output = outputBuilder.ToString();
            var error = errorBuilder.ToString();

            _logger?.LogInformation($"[TunnelService] Update process exited with code: {process.ExitCode}");
            _logger?.LogInformation($"[TunnelService] Output length: {output.Length}, Error length: {error.Length}");
            _logger?.LogInformation($"[TunnelService] Output: {output}");
            if (!string.IsNullOrEmpty(error))
            {
                _logger?.LogError($"[TunnelService] Error: {error}");
            }

            if (process.ExitCode == 0)
            {
                ReportProgress("verifying", "正在验证新版本...", 95);
                await Task.Delay(1000);
                var newVersion = GetCloudflaredVersion();
                _logger?.LogInformation($"[TunnelService] Update successful, new version: {newVersion}");
                ReportProgress("complete", "升级成功！", 100, true, false, newVersion);
                return newVersion ?? "Updated successfully";
            }
            else
            {
                var errorMsg = string.IsNullOrEmpty(error) ? output : error;
                _logger?.LogError($"[TunnelService] Update failed. ExitCode: {process.ExitCode}, Error: {errorMsg}");
                
                if (string.IsNullOrEmpty(errorMsg))
                {
                    errorMsg = $"升级失败 (退出码: {process.ExitCode})。可能需要管理员权限。";
                }
                
                if (errorMsg.Contains("The system cannot find the file specified") || 
                    errorMsg.Contains("Access is denied") ||
                    errorMsg.Contains("permission") ||
                    errorMsg.Contains("denied") ||
                    errorMsg.Contains("管理员") ||
                    process.ExitCode == 1 ||
                    process.ExitCode == 10)
                {
                    errorMsg = "Windows 系统限制导致自动升级失败。请点击\"下载\"按钮手动下载 cloudflared-windows-amd64.exe，然后替换当前文件。";
                }
                
                ReportProgress("error", errorMsg, 0, true, true);
                throw new InvalidOperationException(errorMsg);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error updating cloudflared: {ex.Message}");
            ReportProgress("error", ex.Message, 0, true, true);
            throw;
        }
        finally
        {
            _cachedCloudflaredStatus = null;
            _lastCloudflaredCheck = DateTime.MinValue;
        }
    }

    private void ParseOutput(int port, string line)
    {
        var urlPattern = @"https://[a-z0-9-]+\.trycloudflare\.com";
        var match = Regex.Match(line, urlPattern);

        if (match.Success)
        {
            var url = match.Value;
            
            // 检查进程是否还在运行，如果进程已退出或更换，则允许更新 URL
            var processChanged = false;
            if (_tunnelProcesses.TryGetValue(port, out var process))
            {
                if (process.HasExited)
                {
                    processChanged = true;
                }
            }
            else
            {
                processChanged = true;
            }
            
            // 如果进程没有变化且已有有效 URL，则不覆盖
            if (!processChanged && 
                _tunnels.TryGetValue(port, out var tunnel) && 
                !string.IsNullOrEmpty(tunnel.TunnelUrl) && 
                tunnel.TunnelUrl != "Unknown" &&
                tunnel.TunnelUrl.Contains("trycloudflare.com"))
            {
                // 已有有效 URL 且进程未变化，不覆盖
                _logger?.LogDebug($"[TunnelService] Existing valid URL found for port {port}, not overwriting with new URL: {url}");
                return;
            }
            
            // 如果 URL 发生变化，记录日志
            var oldUrl = _tunnelUrls.GetValueOrDefault(port, string.Empty);
            var isNewUrl = string.IsNullOrEmpty(oldUrl) || oldUrl != url;
            
            if (isNewUrl && !string.IsNullOrEmpty(oldUrl))
            {
                _logger?.LogInformation($"[TunnelService] URL changed for port {port}: {oldUrl} -> {url}");
            }
            
            _tunnelUrls[port] = url;
            
            if (_tunnels.TryGetValue(port, out tunnel))
            {
                tunnel.TunnelUrl = url;
                SaveActiveTunnels();
                
                // 新创建的隧道或 URL 发生变化时发送通知
                if (isNewUrl && _notificationService != null)
                {
                    _notificationService.NotifyTunnelCreated(port, url);
                }
            }
        }

        var lowerLine = line.ToLower();
        if (lowerLine.Contains("error") ||
            lowerLine.Contains("err ") ||
            lowerLine.Contains("fatal") ||
            lowerLine.Contains("failed") ||
            lowerLine.Contains("unable to") ||
            lowerLine.Contains("permission denied") ||
            lowerLine.Contains("could not") ||
            lowerLine.Contains("worker threw exception") ||
            lowerLine.Contains("unmarshaling"))
        {
            _tunnelErrors[port] = line;
        }
    }

    public async Task CleanupOrphanedTunnelsAsync()
    {
        await Task.Run(() =>
        {
            try
            {
                var processes = Process.GetProcessesByName("cloudflared");
                var matchedProcessIds = new HashSet<int>();

                foreach (var kvp in _tunnelProcesses)
                {
                    var process = kvp.Value;
                    if (!process.HasExited)
                    {
                        matchedProcessIds.Add(process.Id);
                    }
                }

                if (OperatingSystem.IsWindows())
                {
                    foreach (var process in processes)
                    {
                        try
                        {
                            if (!matchedProcessIds.Contains(process.Id))
                            {
                                var commandLine = GetProcessCommandLine(process.Id);
                                if (commandLine != null && commandLine.Contains("--url"))
                                {
                                    // Don't kill orphaned cloudflared processes on startup
                                    // process.Kill();
                                    // _logger?.LogInformation($"Killed orphaned cloudflared process (PID: {process.Id})");
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error cleaning up orphaned tunnels: {ex.Message}");
            }
        });
    }

    #region LocalTunnel Support

    private static readonly string[] LocalTunnelPaths = {
        @"C:\Program Files\nodejs\lt.cmd",
        @"C:\Program Files (x86)\nodejs\lt.cmd",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"npm\lt.cmd"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"npm\lt.cmd"),
        @"lt.cmd",
        @"lt",
        @"/usr/local/bin/lt",
        @"/usr/bin/lt",
        @"/usr/local/bin/localtunnel",
        @"localtunnel"
    };

    /// <summary>
    /// 查找 LocalTunnel 可执行文件路径
    /// </summary>
    public string? GetLocalTunnelPath()
    {
        // 首先检查缓存的状态
        if (_cachedCloudflaredStatus != null && DateTime.Now - _lastCloudflaredCheck < TimeSpan.FromMinutes(5))
        {
            // 复用检查逻辑，但检查的是 lt 命令
        }

        foreach (var path in LocalTunnelPaths)
        {
            try
            {
                if (Path.IsPathRooted(path))
                {
                    if (File.Exists(path))
                    {
                        _logger?.LogInformation($"[TunnelService] Found localtunnel at: {path}");
                        return path;
                    }
                }
                else
                {
                    // 尝试在 PATH 中查找
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = OperatingSystem.IsWindows() ? "where" : "which",
                            Arguments = path,
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    var output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(3000);

                    if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                    {
                        var foundPath = output.Trim().Split('\n')[0].Trim();
                        if (File.Exists(foundPath))
                        {
                            _logger?.LogInformation($"[TunnelService] Found localtunnel in PATH: {foundPath}");
                            return foundPath;
                        }
                    }
                }
            }
            catch { }
        }

        // 最后尝试使用 npx
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "where" : "which",
                    Arguments = "npx",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                _logger?.LogInformation($"[TunnelService] npx found, will use npx localtunnel");
                return "npx";
            }
        }
        catch { }

        return null;
    }

    /// <summary>
    /// 检查 LocalTunnel 是否已安装
    /// </summary>
    public bool IsLocalTunnelInstalled()
    {
        return GetLocalTunnelPath() != null;
    }

    /// <summary>
    /// 启动 LocalTunnel 进程
    /// </summary>
    private async Task<Process> StartLocalTunnelAsync(int port, string? tunnelName)
    {
        var ltPath = GetLocalTunnelPath();
        if (ltPath == null)
        {
            throw new InvalidOperationException("LocalTunnel is not installed. Please install it with: npm install -g localtunnel");
        }

        string arguments;
        string fileName;

        // LocalTunnel 不支持自定义密码，密码由服务器动态生成
        if (ltPath == "npx")
        {
            fileName = "npx";
            arguments = $"localtunnel --port {port}";
        }
        else
        {
            fileName = ltPath;
            arguments = $"--port {port}";
        }

        // 如果有指定子域名，添加 --subdomain 参数
        if (!string.IsNullOrEmpty(tunnelName) && tunnelName != $"port-{port}-tunnel")
        {
            var subdomain = tunnelName.Replace($"port-{port}-", "").Replace("-tunnel", "");
            if (!string.IsNullOrEmpty(subdomain))
            {
                arguments += $" --subdomain {subdomain}";
            }
        }

        _logger?.LogInformation($"[TunnelService] Starting localtunnel with arguments: {arguments}");
        _logger?.LogInformation($"[TunnelService] LocalTunnel path: {fileName}");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(ltPath) ?? Environment.CurrentDirectory
            }
        };

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _logger?.LogInformation($"[TunnelService] LocalTunnel Output: {e.Data}");
                ParseLocalTunnelOutput(port, e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                var lowerLine = e.Data.ToLower();
                if (lowerLine.Contains("err") ||
                    lowerLine.Contains("error") ||
                    lowerLine.Contains("fatal") ||
                    lowerLine.Contains("failed"))
                {
                    _logger?.LogError($"[TunnelService] LocalTunnel Error: {e.Data}");
                }
                else
                {
                    _logger?.LogInformation($"[TunnelService] LocalTunnel: {e.Data}");
                }
                ParseLocalTunnelOutput(port, e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            _tunnelProcesses[port] = process;

            await Task.Delay(1000);

            if (process.HasExited)
            {
                throw new InvalidOperationException($"LocalTunnel process exited with code {process.ExitCode}");
            }

            _logger?.LogInformation($"[TunnelService] LocalTunnel process started with ID: {process.Id}");
            return process;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[TunnelService] Failed to start LocalTunnel: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// 解析 LocalTunnel 输出，提取 URL
    /// </summary>
    private void ParseLocalTunnelOutput(int port, string line)
    {
        _logger?.LogDebug($"[TunnelService] Parsing LocalTunnel output for port {port}: {line}");
        
        // LocalTunnel URL 格式: https://something.loca.lt
        // 也可能输出: your url is: https://something.loca.lt
        // 或者: url: https://something.loca.lt
        var urlPattern = @"https://[a-z0-9-]+\.loca\.lt";
        var match = Regex.Match(line, urlPattern, RegexOptions.IgnoreCase);

        if (match.Success)
        {
            var url = match.Value;
            _logger?.LogInformation($"[TunnelService] LocalTunnel URL found for port {port}: {url}");

            var oldUrl = _tunnelUrls.GetValueOrDefault(port, string.Empty);
            var isNewUrl = string.IsNullOrEmpty(oldUrl) || oldUrl != url;

            _tunnelUrls[port] = url;
            _logger?.LogDebug($"[TunnelService] Updated _tunnelUrls[{port}] = {url}");

            if (_tunnels.TryGetValue(port, out var tunnel))
            {
                tunnel.TunnelUrl = url;
                SaveActiveTunnels();
                _logger?.LogInformation($"[TunnelService] Saved LocalTunnel URL for port {port}: {url}");

                if (isNewUrl && _notificationService != null)
                {
                    _notificationService.NotifyTunnelCreated(port, url);
                }
            }
            else
            {
                _logger?.LogWarning($"[TunnelService] Tunnel not found in _tunnels for port {port}");
            }
        }

        // 检测错误
        var lowerLine = line.ToLower();
        if (lowerLine.Contains("error") ||
            lowerLine.Contains("err ") ||
            lowerLine.Contains("failed") ||
            lowerLine.Contains("unable to") ||
            lowerLine.Contains("could not") ||
            lowerLine.Contains("econnrefused") ||
            lowerLine.Contains("tunnel failed") ||
            lowerLine.Contains("could not connect"))
        {
            _tunnelErrors[port] = line;
            _logger?.LogError($"[TunnelService] LocalTunnel error for port {port}: {line}");
        }
    }

    /// <summary>
    /// 获取 LocalTunnel 密码（当前机器的公网 IP）
    /// </summary>
    private async Task<string?> GetLocalTunnelPasswordAsync()
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(10);
            var password = await httpClient.GetStringAsync("https://loca.lt/mytunnelpassword");
            password = password.Trim();
            _logger?.LogInformation($"[TunnelService] LocalTunnel password retrieved: {password}");
            return password;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[TunnelService] Failed to get LocalTunnel password: {ex.Message}");
            return "(获取失败，请访问链接查看)";
        }
    }

    #endregion
}

