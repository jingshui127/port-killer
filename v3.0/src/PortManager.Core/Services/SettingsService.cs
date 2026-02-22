using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PortManager.Models;
using Microsoft.Extensions.Logging;

namespace PortManager.Services;

public class SettingsService
{
    private const string AppName = "PortManager";
    private const string SettingsFileName = "settings.json";
    private readonly string _settingsPath;
    private readonly ILogger<SettingsService>? _logger;

    public SettingsService(ILogger<SettingsService>? logger = null)
    {
        _logger = logger;
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppName);
        
        Directory.CreateDirectory(appDataPath);
        _settingsPath = Path.Combine(appDataPath, SettingsFileName);
        _logger?.LogInformation($"[SettingsService] Settings path: {_settingsPath}");
    }

    private class SettingsData
    {
        public List<int>? Favorites { get; set; }
        public List<WatchedPort>? WatchedPorts { get; set; }
        public List<CloudflareTunnel>? ActiveTunnels { get; set; }
    }

    private SettingsData LoadSettingsData()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                _logger?.LogDebug($"[SettingsService] Loaded JSON content: {json}");
                return JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[SettingsService] Error loading settings: {ex.Message}");
        }
        return new SettingsData();
    }

    private void SaveSettingsData(SettingsData data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
            _logger?.LogDebug($"[SettingsService] Saved JSON content: {json}");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[SettingsService] Error saving settings: {ex.Message}");
        }
    }

    public HashSet<int> GetFavorites()
    {
        var data = LoadSettingsData();
        return data.Favorites != null ? new HashSet<int>(data.Favorites) : new HashSet<int>();
    }

    public void SaveFavorites(HashSet<int> favorites)
    {
        var data = LoadSettingsData();
        data.Favorites = favorites.ToList();
        SaveSettingsData(data);
    }

    public List<WatchedPort> GetWatchedPorts()
    {
        var data = LoadSettingsData();
        return data.WatchedPorts ?? new List<WatchedPort>();
    }

    public void SaveWatchedPorts(List<WatchedPort> watchedPorts)
    {
        var data = LoadSettingsData();
        data.WatchedPorts = watchedPorts;
        SaveSettingsData(data);
    }

    public List<CloudflareTunnel> GetActiveTunnels()
    {
        var data = LoadSettingsData();
        var tunnels = data.ActiveTunnels ?? new List<CloudflareTunnel>();
        
        _logger?.LogInformation($"[SettingsService] Loaded {tunnels.Count} tunnels from settings");
        foreach (var tunnel in tunnels)
        {
            _logger?.LogInformation($"[SettingsService] Loaded tunnel - Port: {tunnel.Port}, URL: {tunnel.TunnelUrl}, Status: {tunnel.Status}");
        }
        
        return tunnels;
    }

    public void SaveActiveTunnels(List<CloudflareTunnel> tunnels)
    {
        var data = LoadSettingsData();
        data.ActiveTunnels = tunnels;
        SaveSettingsData(data);
        
        _logger?.LogInformation($"[SettingsService] Saved {tunnels.Count} tunnels to settings file");
    }

    public void ClearAllSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                File.Delete(_settingsPath);
            }
        }
        catch
        {
        }
    }
}

