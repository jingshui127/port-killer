using PortManager.Models;
using PortManager.Services;
using System.Text.Json;

namespace PortManager.Core.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly string _testSettingsPath;
    private readonly SettingsService _settingsService;

    public SettingsServiceTests()
    {
        // 使用临时文件作为测试配置文件
        _testSettingsPath = Path.Combine(Path.GetTempPath(), $"test_settings_{Guid.NewGuid()}.json");
        _settingsService = new SettingsService(null);
        
        // 通过反射设置私有字段
        var settingsPathField = typeof(SettingsService).GetField("_settingsPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        settingsPathField?.SetValue(_settingsService, _testSettingsPath);
    }

    public void Dispose()
    {
        // 清理测试文件
        if (File.Exists(_testSettingsPath))
        {
            File.Delete(_testSettingsPath);
        }
    }

    [Fact]
    public void SaveAndGetFavorites_RoundTrip()
    {
        // Arrange
        var favorites = new HashSet<int> { 8080, 3000, 5000 };

        // Act
        _settingsService.SaveFavorites(favorites);
        var result = _settingsService.GetFavorites();

        // Assert
        Assert.Equal(favorites.Count, result.Count);
        Assert.Equal(favorites.OrderBy(x => x), result.OrderBy(x => x));
    }

    [Fact]
    public void SaveAndGetActiveTunnels_RoundTrip()
    {
        // Arrange
        var tunnels = new List<Tunnel>
        {
            new Tunnel
            {
                Port = 8080,
                TunnelUrl = "https://test.trycloudflare.com",
                Status = "Active",
                TunnelName = "test-tunnel",
                Provider = TunnelProvider.Cloudflare
            },
            new Tunnel
            {
                Port = 3000,
                TunnelUrl = "https://test.loca.lt",
                Status = "Active",
                TunnelName = "localtunnel-test",
                Provider = TunnelProvider.LocalTunnel,
                Password = "test-password"
            }
        };

        // Act
        _settingsService.SaveActiveTunnels(tunnels);
        var result = _settingsService.GetActiveTunnels();

        // Assert
        Assert.Equal(tunnels.Count, result.Count);
        
        var firstTunnel = result.First(t => t.Port == 8080);
        Assert.Equal("https://test.trycloudflare.com", firstTunnel.TunnelUrl);
        Assert.Equal(TunnelProvider.Cloudflare, firstTunnel.Provider);
        
        var secondTunnel = result.First(t => t.Port == 3000);
        Assert.Equal("https://test.loca.lt", secondTunnel.TunnelUrl);
        Assert.Equal(TunnelProvider.LocalTunnel, secondTunnel.Provider);
        Assert.Equal("test-password", secondTunnel.Password);
    }

    [Fact]
    public void GetActiveTunnels_ReturnsEmptyList_WhenFileDoesNotExist()
    {
        // Arrange
        var nonExistentService = new SettingsService(null);
        var settingsPathField = typeof(SettingsService).GetField("_settingsPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        settingsPathField?.SetValue(nonExistentService, "/non/existent/path/settings.json");

        // Act
        var result = nonExistentService.GetActiveTunnels();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetFavorites_ReturnsEmptyList_WhenFileDoesNotExist()
    {
        // Arrange
        var nonExistentService = new SettingsService(null);
        var settingsPathField = typeof(SettingsService).GetField("_settingsPath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        settingsPathField?.SetValue(nonExistentService, "/non/existent/path/settings.json");

        // Act
        var result = nonExistentService.GetFavorites();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void ClearAllSettings_RemovesAllData()
    {
        // Arrange
        var favorites = new HashSet<int> { 8080, 3000 };
        var tunnels = new List<Tunnel>
        {
            new Tunnel { Port = 8080, TunnelUrl = "https://test.com", Status = "Active" }
        };
        
        _settingsService.SaveFavorites(favorites);
        _settingsService.SaveActiveTunnels(tunnels);

        // Act
        _settingsService.ClearAllSettings();

        // Assert
        Assert.Empty(_settingsService.GetFavorites());
        Assert.Empty(_settingsService.GetActiveTunnels());
    }

    [Fact]
    public void SaveActiveTunnels_PreservesAllProperties()
    {
        // Arrange
        var tunnel = new Tunnel
        {
            Port = 8080,
            TunnelUrl = "https://example.trycloudflare.com",
            Status = "Active",
            LastError = null,
            StartTime = new DateTime(2024, 1, 15, 10, 30, 0),
            ProcessId = 12345,
            Uptime = "2小时30分钟",
            TunnelName = "my-tunnel",
            Provider = TunnelProvider.Cloudflare,
            Password = null
        };

        // Act
        _settingsService.SaveActiveTunnels(new List<Tunnel> { tunnel });
        var result = _settingsService.GetActiveTunnels().First();

        // Assert
        Assert.Equal(tunnel.Port, result.Port);
        Assert.Equal(tunnel.TunnelUrl, result.TunnelUrl);
        Assert.Equal(tunnel.Status, result.Status);
        Assert.Equal(tunnel.TunnelName, result.TunnelName);
        Assert.Equal(tunnel.Provider, result.Provider);
    }
}
