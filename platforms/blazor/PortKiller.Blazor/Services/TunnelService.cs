using PortKiller.Blazor.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace PortKiller.Blazor.Services;

public class TunnelService
{
    private readonly ConcurrentDictionary<int, CloudflareTunnel> _tunnels = new();
    private readonly ConcurrentDictionary<int, Process> _tunnelProcesses = new();
    private readonly ConcurrentDictionary<int, string> _tunnelUrls = new();
    private readonly ConcurrentDictionary<int, string> _tunnelErrors = new();
    private readonly ILogger<TunnelService>? _logger;
    private readonly SettingsService _settingsService;

    public event EventHandler<CloudflaredUpdateProgress>? UpdateProgressChanged;

    private static readonly string[] CloudflaredPaths = {
        @"C:\Program Files\cloudflared\cloudflared.exe",
        @"C:\Program Files (x86)\cloudflared\cloudflared.exe",
        @"C:\ProgramData\chocolatey\bin\cloudflared.exe",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"cloudflared\cloudflared.exe"),
        @"cloudflared.exe"
    };

    public static bool IsRunningAsAdministrator()
    {
        if (OperatingSystem.IsWindows())
        {
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
        }
        return false;
    }

    public TunnelService(ILogger<TunnelService>? logger = null)
    {
        _logger = logger;
        _settingsService = new SettingsService();
    }

    public async Task InitializeAsync()
    {
        await CleanupOrphanedTunnelsAsync();
        
        var savedTunnels = _settingsService.GetActiveTunnels();
        
        foreach (var savedTunnel in savedTunnels)
        {
            var existingProcess = GetProcessForPort(savedTunnel.Port);
            
            if (existingProcess != null)
            {
                var tunnel = new CloudflareTunnel
                {
                    Port = savedTunnel.Port,
                    Status = "Active",
                    TunnelUrl = savedTunnel.TunnelUrl,
                    ProcessId = existingProcess.Id,
                    StartTime = existingProcess.StartTime,
                    TunnelName = savedTunnel.TunnelName
                };
                
                _tunnels[tunnel.Port] = tunnel;
                _tunnelProcesses[tunnel.Port] = existingProcess;
            }
            else
            {
                _ = RestartTunnelOnStartupAsync(savedTunnel);
            }
        }
    }

    private async Task RestartTunnelOnStartupAsync(CloudflareTunnel savedTunnel)
    {
        try
        {
            _logger?.LogInformation($"[TunnelService] Auto-restarting tunnel for port {savedTunnel.Port}");
            
            var newTunnel = new CloudflareTunnel
            {
                Port = savedTunnel.Port,
                Status = "Starting",
                StartTime = DateTime.Now,
                TunnelName = savedTunnel.TunnelName ?? $"port-{savedTunnel.Port}-tunnel",
                TunnelUrl = string.Empty
            };

            _tunnels[savedTunnel.Port] = newTunnel;
            SaveActiveTunnels();

            var cloudflaredProcess = await StartCloudflaredAsync(savedTunnel.Port, newTunnel.TunnelName);
            newTunnel.ProcessId = cloudflaredProcess.Id;
            newTunnel.Status = "Active";
            
            _tunnelUrls[savedTunnel.Port] = string.Empty;
            _tunnelErrors[savedTunnel.Port] = string.Empty;
            
            var timeout = TimeSpan.FromSeconds(30);
            var startTime = DateTime.Now;

            while (DateTime.Now - startTime < timeout)
            {
                if (!string.IsNullOrEmpty(_tunnelUrls[savedTunnel.Port]))
                {
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
                    return;
                }
                
                if (cloudflaredProcess.HasExited)
                {
                    _logger?.LogError($"[TunnelService] Cloudflared process exited with code {cloudflaredProcess.ExitCode}");
                    newTunnel.Status = "Error";
                    newTunnel.LastError = $"Process exited with code {cloudflaredProcess.ExitCode}";
                    return;
                }
                
                await Task.Delay(500);
            }
            
            _logger?.LogError($"[TunnelService] Timeout waiting for tunnel URL");
            newTunnel.Status = "Error";
            newTunnel.LastError = "Timeout waiting for tunnel URL";
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[TunnelService] Error auto-restarting tunnel: {ex.Message}");
            if (_tunnels.TryGetValue(savedTunnel.Port, out var tunnel))
            {
                tunnel.Status = "Error";
                tunnel.LastError = ex.Message;
            }
        }
    }

    public List<CloudflareTunnel> GetTunnels()
    {
        return _tunnels.Values.ToList();
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public Process? GetProcessForPort(int port)
    {
        if (!OperatingSystem.IsWindows())
        {
            foreach (var kvp in _tunnelProcesses)
            {
                var process = kvp.Value;
                if (!process.HasExited)
                {
                    return process;
                }
            }
            return null;
        }
        
        foreach (var kvp in _tunnelProcesses)
        {
            var process = kvp.Value;
            if (!process.HasExited)
            {
                var commandLine = GetProcessCommandLine(process.Id);
                if (commandLine != null && commandLine.Contains($"--url localhost:{port}"))
                {
                    return process;
                }
            }
        }
        
        var allCloudflaredProcesses = Process.GetProcessesByName("cloudflared");
        foreach (var process in allCloudflaredProcesses)
        {
            try
            {
                var commandLine = GetProcessCommandLine(process.Id);
                if (commandLine != null && commandLine.Contains($"--url localhost:{port}"))
                {
                    return process;
                }
            }
            catch
            {
            }
        }
        
        return null;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public string? GetProcessCommandLine(int processId)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }
        
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

    public CloudflareTunnel? GetTunnel(int port)
    {
        return _tunnels.GetValueOrDefault(port);
    }

    public async Task<CloudflareTunnel> CreateTunnelAsync(int port, string? tunnelName = null)
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
            TunnelUrl = string.Empty
        };

        _tunnels[port] = tunnel;
        SaveActiveTunnels();

        try
        {
            var cloudflaredProcess = await StartCloudflaredAsync(port, stableTunnelName);
            tunnel.ProcessId = cloudflaredProcess.Id;
            tunnel.Status = "Active";
            
            _tunnelUrls[port] = string.Empty;
            _tunnelErrors[port] = string.Empty;
            
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
        if (!_tunnels.ContainsKey(port))
        {
            throw new InvalidOperationException($"No tunnel found for port {port}");
        }

        if (_tunnelProcesses.TryRemove(port, out var process))
        {
            try
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error stopping tunnel process: {ex.Message}");
            }
        }

        _tunnelUrls.TryRemove(port, out _);
        _tunnelErrors.TryRemove(port, out _);

        _tunnels.TryRemove(port, out _);
        SaveActiveTunnels();
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
        _settingsService.SaveActiveTunnels(activeTunnels);
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
        }
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
            httpClient.DefaultRequestHeaders.Add("User-Agent", "PortKiller");
            
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

        return new CloudflaredStatus
        {
            IsInstalled = isInstalled,
            Version = currentVersion,
            LatestVersion = latestVersion,
            HasUpdate = hasUpdate
        };
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
            var hasReceivedOutput = false;

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    hasReceivedOutput = true;
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
    }

    private void ParseOutput(int port, string line)
    {
        var urlPattern = @"https://[a-z0-9-]+\.trycloudflare\.com";
        var match = Regex.Match(line, urlPattern);

        if (match.Success)
        {
            var url = match.Value;
            _tunnelUrls[port] = url;
            
            if (_tunnels.TryGetValue(port, out var tunnel))
            {
                tunnel.TunnelUrl = url;
                SaveActiveTunnels();
            }
        }

        var lowerLine = line.ToLower();
        if (lowerLine.Contains("error") || 
            lowerLine.Contains("failed") || 
            lowerLine.Contains("unable to") ||
            lowerLine.Contains("permission denied") ||
            lowerLine.Contains("could not"))
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
                        finally
                        {
                            process.Dispose();
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
}
