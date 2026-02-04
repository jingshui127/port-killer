using PortKiller.Blazor.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace PortKiller.Blazor.Services;

public class TunnelService
{
    private readonly ConcurrentDictionary<int, CloudflareTunnel> _tunnels = new();
    private readonly ConcurrentDictionary<int, Process> _tunnelProcesses = new();
    private readonly ConcurrentDictionary<int, string> _tunnelUrls = new();
    private readonly ConcurrentDictionary<int, string> _tunnelErrors = new();
    private readonly ILogger<TunnelService>? _logger;
    private readonly SettingsService _settingsService;

    private static readonly string[] CloudflaredPaths = {
        @"C:\Program Files\cloudflared\cloudflared.exe",
        @"C:\Program Files (x86)\cloudflared\cloudflared.exe",
        @"C:\ProgramData\chocolatey\bin\cloudflared.exe",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"cloudflared\cloudflared.exe"),
        @"cloudflared.exe"
    };

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
            
            var tunnel = new CloudflareTunnel
            {
                Port = savedTunnel.Port,
                Status = existingProcess != null ? "Active" : "Inactive",
                TunnelUrl = savedTunnel.TunnelUrl,
                ProcessId = existingProcess?.Id ?? 0,
                StartTime = existingProcess != null ? existingProcess.StartTime : savedTunnel.StartTime,
                TunnelName = savedTunnel.TunnelName
            };
            
            _tunnels[tunnel.Port] = tunnel;
            if (existingProcess != null)
            {
                _tunnelProcesses[tunnel.Port] = existingProcess;
            }
        }
    }

    public List<CloudflareTunnel> GetTunnels()
    {
        return _tunnels.Values.ToList();
    }

    public Process? GetProcessForPort(int port)
    {
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

    public string? GetProcessCommandLine(int processId)
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
            TunnelName = stableTunnelName
        };

        _tunnels[port] = tunnel;
        SaveActiveTunnels();

        try
        {
            var cloudflaredProcess = await StartCloudflaredAsync(port, stableTunnelName);
            tunnel.ProcessId = cloudflaredProcess.Id;
            tunnel.Status = "Active";
            
            _tunnelUrls[port] = "";
            _tunnelErrors[port] = "";
            
            var timeout = TimeSpan.FromSeconds(20);
            var startTime = DateTime.Now;

            while (DateTime.Now - startTime < timeout)
            {
                if (!string.IsNullOrEmpty(_tunnelUrls[port]))
                {
                    tunnel.TunnelUrl = _tunnelUrls[port];
                    SaveActiveTunnels();
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

        return tunnel;
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
                _logger?.LogError($"[TunnelService] Error: {e.Data}");
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

    private void SaveActiveTunnels()
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
                StartTime = DateTime.Now
            };

            _tunnels[port] = newTunnel;
            SaveActiveTunnels();

            var cloudflaredProcess = await StartCloudflaredAsync(port, null);
            newTunnel.ProcessId = cloudflaredProcess.Id;
            newTunnel.Status = "Active";
            
            _tunnelUrls[port] = "";
            _tunnelErrors[port] = "";
            
            var timeout = TimeSpan.FromSeconds(20);
            var startTime = DateTime.Now;

            while (DateTime.Now - startTime < timeout)
            {
                if (!string.IsNullOrEmpty(_tunnelUrls[port]))
                {
                    newTunnel.TunnelUrl = _tunnelUrls[port];
                    SaveActiveTunnels();
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
            tunnel.Status = "Error";
            tunnel.LastError = ex.Message;
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

    private void ParseOutput(int port, string line)
    {
        var urlPattern = @"https://[a-z0-9-]+\.trycloudflare\.com";
        var match = Regex.Match(line, urlPattern);

        if (match.Success)
        {
            var url = match.Value;
            _tunnelUrls[port] = url;
            
            // 更新_tunnels字典中的隧道对象中的TunnelUrl属性
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
            lowerLine.Contains("permission denied"))
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
            catch (Exception ex)
            {
                _logger?.LogError($"Error cleaning up orphaned tunnels: {ex.Message}");
            }
        });
    }
}
