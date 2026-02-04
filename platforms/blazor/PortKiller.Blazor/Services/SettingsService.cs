using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PortKiller.Blazor.Models;

namespace PortKiller.Blazor.Services;

public class SettingsService
{
    private const string AppName = "PortKiller.Blazor";
    private const string SettingsFileName = "settings.json";
    private readonly string _settingsPath;

    public SettingsService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppName);
        
        Directory.CreateDirectory(appDataPath);
        _settingsPath = Path.Combine(appDataPath, SettingsFileName);
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
                return JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
            }
        }
        catch
        {
        }
        return new SettingsData();
    }

    private void SaveSettingsData(SettingsData data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
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
        return data.ActiveTunnels ?? new List<CloudflareTunnel>();
    }

    public void SaveActiveTunnels(List<CloudflareTunnel> tunnels)
    {
        var data = LoadSettingsData();
        data.ActiveTunnels = tunnels;
        SaveSettingsData(data);
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
