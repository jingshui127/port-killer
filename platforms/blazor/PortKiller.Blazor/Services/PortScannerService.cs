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
    private readonly ILogger<PortScannerService> _logger;

    public PortScannerService(SettingsService settingsService, ILogger<PortScannerService> logger)
    {
        _settingsService = settingsService;
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
            var processedPorts = new HashSet<int>();
            
            try
            {
                var activeListeners = IPGlobalProperties.GetIPGlobalProperties()
                    .GetActiveTcpListeners()
                    .Where(listener => listener.Port > 0)
                    .ToList();

                foreach (var listener in activeListeners)
                {
                    var port = listener.Port;
                    
                    // 跳过已处理的端口，避免重复
                    if (processedPorts.Contains(port))
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
                    
                    newPorts.Add(new PortInfo
                    {
                        Port = port,
                        ProcessName = processName,
                        Pid = pid,
                        Address = address,
                        User = Environment.UserName,
                        Command = command,
                        IsActive = true,
                        IsFavorite = _favorites.ContainsKey(port),
                        IsWatched = _watched.ContainsKey(port)
                    });
                    
                    processedPorts.Add(port);
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
        if (portInfo == null) return;

        try
        {
            var process = Process.GetProcessById(portInfo.Pid);
            if (process != null)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
                _ports.Remove(portInfo);
            }
        }
        catch (ArgumentException)
        {
            _logger.LogWarning("Process with PID {Pid} not found", portInfo.Pid);
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogWarning(ex, "Access denied when killing process {Pid}. Please run the application as Administrator to kill processes", portInfo.Pid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error killing process");
        }
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
