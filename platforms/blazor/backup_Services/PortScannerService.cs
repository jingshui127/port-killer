using PortKiller.Blazor.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace PortKiller.Blazor.Services;

public class PortScannerService
{
    private readonly List<PortInfo> _ports = new();
    private readonly Dictionary<int, bool> _favorites = new();
    private readonly Dictionary<int, bool> _watched = new();

    public List<PortInfo> GetPorts()
    {
        return _ports;
    }

    public async Task RefreshPortsAsync()
    {
        await Task.Run(() =>
        {
            _ports.Clear();
            
            try
            {
                var activeListeners = IPGlobalProperties.GetIPGlobalProperties()
                    .GetActiveTcpListeners()
                    .Where(listener => listener.Port > 0)
                    .ToList();

                foreach (var listener in activeListeners)
                {
                    var port = listener.Port;
                    var address = listener.Address?.ToString() ?? "127.0.0.1";
                    
                    var process = GetProcessForPort(port);
                    
                    _ports.Add(new PortInfo
                    {
                        Port = port,
                        ProcessName = process?.ProcessName ?? "Unknown",
                        Pid = process?.Id ?? 0,
                        Address = address,
                        User = Environment.UserName,
                        Command = process?.MainModule?.FileName ?? string.Empty,
                        IsActive = true,
                        IsFavorite = _favorites.ContainsKey(port),
                        IsWatched = _watched.ContainsKey(port)
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning ports: {ex.Message}");
            }
        });
    }

    private Process? GetProcessForPort(int port)
    {
        try
        {
            var processes = Process.GetProcesses();
            return processes.FirstOrDefault(p => p.MainModule?.FileName != null);
        }
        catch { }

        return null;
    }

    public void KillPort(int port)
    {
        var portInfo = _ports.FirstOrDefault(p => p.Port == port);
        if (portInfo == null) return;

        try
        {
            var process = Process.GetProcessById(portInfo.Pid);
            process?.Kill();
            _ports.Remove(portInfo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error killing process: {ex.Message}");
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
    }

    public void ToggleWatch(int port)
    {
        var portInfo = _ports.FirstOrDefault(p => p.Port == port);
        if (portInfo == null) return;

        if (_watched.ContainsKey(port))
        {
            _watched.Remove(port);
            portInfo.IsWatched = false;
        }
        else
        {
            _watched[port] = true;
            portInfo.IsWatched = true;
        }
    }
}
