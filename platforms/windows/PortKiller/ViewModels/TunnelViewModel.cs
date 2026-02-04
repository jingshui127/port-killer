using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using PortKiller.Models;
using PortKiller.Services;

namespace PortKiller.ViewModels;

/// <summary>
/// ViewModel for managing Cloudflare tunnels
/// </summary>
public class TunnelViewModel : INotifyPropertyChanged
{
    private readonly TunnelService _tunnelService;
    private readonly NotificationService _notificationService;
    private readonly SettingsService _settingsService;
    private readonly DispatcherTimer _uptimeTimer;
    private bool _isCloudflaredInstalled;
    private bool _isInitializing;
    private bool _isRefreshing;

    public ObservableCollection<CloudflareTunnel> Tunnels { get; }

    public bool IsCloudflaredInstalled
    {
        get => _isCloudflaredInstalled;
        set => SetField(ref _isCloudflaredInstalled, value);
    }

    public bool IsInitializing
    {
        get => _isInitializing;
        set => SetField(ref _isInitializing, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetField(ref _isRefreshing, value);
    }

    public int ActiveTunnelCount => Tunnels.Count(t => t.Status == TunnelStatus.Active);

    public TunnelViewModel(TunnelService tunnelService, NotificationService notificationService, SettingsService settingsService)
    {
        _tunnelService = tunnelService;
        _notificationService = notificationService;
        _settingsService = settingsService;
        Tunnels = new ObservableCollection<CloudflareTunnel>();

        // Check if cloudflared is installed
        IsCloudflaredInstalled = _tunnelService.IsCloudflaredInstalled;

        // Set up timer to update uptime every second
        _uptimeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _uptimeTimer.Tick += (s, e) => UpdateUptimes();
        _uptimeTimer.Start();
    }

    /// <summary>
    /// Initializes the tunnel view model and restores saved tunnels
    /// </summary>
    public async Task InitializeAsync()
    {
        // Set initializing state
        IsInitializing = true;
        OnPropertyChanged(nameof(IsInitializing));

        // Load saved tunnels from settings
        var savedTunnels = _settingsService.GetActiveTunnels();
        
        // Check for existing cloudflared processes first
        var existingProcesses = Process.GetProcessesByName("cloudflared");
        var matchedProcessIds = new HashSet<int>();

        // Restore each saved tunnel
        foreach (var savedTunnel in savedTunnels)
        {
            // Check if port is already in use by existing cloudflared process
            var existingProcess = _tunnelService.GetProcessForPort(savedTunnel.Port);
            
            if (existingProcess != null)
            {
                // Port is already in use, connect to existing process
                Debug.WriteLine($"Found existing cloudflared process for port {savedTunnel.Port}");
                matchedProcessIds.Add(existingProcess.Id);
                
                // Notify user that we're reusing existing tunnel
                _notificationService.Notify(
                    "隧道已恢复",
                    $"端口 {savedTunnel.Port} 的隧道已恢复，使用现有进程"
                );
                
                // Create tunnel object and use saved URL if available
                var tunnel = new CloudflareTunnel(savedTunnel.Port)
                {
                    Status = TunnelStatus.Active,
                    TunnelUrl = savedTunnel.TunnelUrl, // Re-use saved URL
                    ProcessId = existingProcess.Id,
                    StartTime = existingProcess.StartTime
                };
                
                Tunnels.Add(tunnel);
                
                // Attach to existing process
                _tunnelService.AttachTunnel(tunnel, existingProcess);
            }
            else
            {
                // Port is not in use, start new tunnel
                var tunnel = new CloudflareTunnel(savedTunnel.Port)
                {
                    Status = TunnelStatus.Starting
                };
                
                Tunnels.Add(tunnel);
                
                // Start the tunnel
                try
                {
                    await StartTunnelInternalAsync(tunnel);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to restore tunnel for port {tunnel.Port}: {ex.Message}");
                    tunnel.Status = TunnelStatus.Error;
                    tunnel.LastError = ex.Message;
                }
            }
        }

        // Clean up ONLY the cloudflared processes that aren't being managed by us
        await Task.Run(() => {
            foreach (var process in existingProcesses)
            {
                try {
                    if (!matchedProcessIds.Contains(process.Id)) {
                        // Check if it's a tunnel process
                        var commandLine = _tunnelService.GetProcessCommandLine(process.Id);
                        if (commandLine != null && commandLine.Contains("tunnel") && commandLine.Contains("--url"))
                        {
                            process.Kill();
                            Debug.WriteLine($"Killed truly orphaned cloudflared process (PID: {process.Id})");
                        }
                    }
                } catch { }
            }
        });

        // Clear initializing state
        IsInitializing = false;
        OnPropertyChanged(nameof(IsInitializing));
        OnPropertyChanged(nameof(ActiveTunnelCount));
    }

    /// <summary>
    /// Starts a tunnel internally (without duplicate check)
    /// </summary>
    private async Task StartTunnelInternalAsync(CloudflareTunnel tunnel)
    {
        if (!IsCloudflaredInstalled)
        {
            MessageBox.Show(
                "未安装 cloudflared。请安装它以使用 Cloudflare 隧道。\n\n" +
                "安装选项：\n" +
                "1. 从以下地址下载：https://github.com/cloudflare/cloudflared/releases\n" +
                "2. 或使用 Chocolatey：choco install cloudflared",
                "找不到 cloudflared",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        // Set up handlers
        _tunnelService.SetUrlHandler(tunnel.Id, url =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                tunnel.TunnelUrl = url;
                tunnel.Status = TunnelStatus.Active;
                tunnel.StartTime = DateTime.Now;

                // Auto-copy URL to clipboard
                CopyUrlToClipboard(url);

                // Send notification
                _notificationService.Notify(
                    "隧道已激活",
                    $"端口 {tunnel.Port} 现在可以公开访问：\n{ShortenUrl(url)}");

                // Save active tunnels to settings
                SaveActiveTunnels();

                OnPropertyChanged(nameof(ActiveTunnelCount));
            });
        });

        _tunnelService.SetErrorHandler(tunnel.Id, error =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                tunnel.LastError = error;
                if (tunnel.Status != TunnelStatus.Active)
                {
                    tunnel.Status = TunnelStatus.Error;
                }
                Debug.WriteLine($"Tunnel error: {error}");
            });
        });

        try
        {
            await _tunnelService.StartTunnelAsync(tunnel);

            // Wait a bit to see if URL is detected
            await Task.Delay(3000);

            if (tunnel.Status == TunnelStatus.Starting)
            {
                // Still starting, URL should appear soon
                Debug.WriteLine($"Tunnel for port {tunnel.Port} is starting, waiting for URL...");
            }
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                tunnel.Status = TunnelStatus.Error;
                tunnel.LastError = ex.Message;
                
                MessageBox.Show(
                    $"Failed to start tunnel for port {tunnel.Port}:\n{ex.Message}",
                    "Tunnel Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            });
        }
    }

    /// <summary>
    /// Starts a new tunnel for the specified port
    /// </summary>
    public async Task StartTunnelAsync(int port)
    {
        if (!IsCloudflaredInstalled)
        {
            MessageBox.Show(
                "未安装 cloudflared。请安装它以使用 Cloudflare 隧道。\n\n" +
                "安装选项：\n" +
                "1. 从以下地址下载：https://github.com/cloudflare/cloudflared/releases\n" +
                "2. 或使用 Chocolatey：choco install cloudflared",
                "找不到 cloudflared",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        // Check if tunnel already exists for this port
        var existingTunnel = Tunnels.FirstOrDefault(t => t.Port == port && t.Status != TunnelStatus.Error);
        if (existingTunnel != null)
        {
            // Already tunneling this port - just copy the URL if available
            if (!string.IsNullOrEmpty(existingTunnel.TunnelUrl))
            {
                CopyUrlToClipboard(existingTunnel.TunnelUrl);
            }
            return;
        }

        var tunnel = new CloudflareTunnel(port)
        {
            Status = TunnelStatus.Starting
        };

        Application.Current.Dispatcher.Invoke(() => 
        {
            Tunnels.Add(tunnel);
            SaveActiveTunnels();
        });

        // Set up handlers
        _tunnelService.SetUrlHandler(tunnel.Id, url =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                tunnel.TunnelUrl = url;
                tunnel.Status = TunnelStatus.Active;
                tunnel.StartTime = DateTime.Now;

                // Auto-copy URL to clipboard
                CopyUrlToClipboard(url);

                // Send notification
                _notificationService.Notify(
                    "隧道已激活",
                    $"端口 {tunnel.Port} 现在可以公开访问：\n{ShortenUrl(url)}");

                // Save active tunnels to settings
                SaveActiveTunnels();

                OnPropertyChanged(nameof(ActiveTunnelCount));
            });
        });

        _tunnelService.SetErrorHandler(tunnel.Id, error =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                tunnel.LastError = error;
                if (tunnel.Status != TunnelStatus.Active)
                {
                    tunnel.Status = TunnelStatus.Error;
                }
                Debug.WriteLine($"Tunnel error: {error}");
            });
        });

        try
        {
            await _tunnelService.StartTunnelAsync(tunnel);

            // Wait a bit to see if URL is detected
            await Task.Delay(3000);

            if (tunnel.Status == TunnelStatus.Starting)
            {
                // Still starting, URL should appear soon
                Debug.WriteLine($"Tunnel for port {port} is starting, waiting for URL...");
            }
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                tunnel.Status = TunnelStatus.Error;
                tunnel.LastError = ex.Message;
                
                MessageBox.Show(
                    $"为端口 {port} 启动隧道失败：\n{ex.Message}",
                    "隧道错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            });
        }
    }

    /// <summary>
    /// Stops a tunnel
    /// </summary>
    public async Task StopTunnelAsync(CloudflareTunnel tunnel)
    {
        tunnel.Status = TunnelStatus.Stopping;

        try
        {
            await _tunnelService.StopTunnelAsync(tunnel.Id);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Tunnels.Remove(tunnel);
                OnPropertyChanged(nameof(ActiveTunnelCount));
                
                // Save updated tunnels list
                SaveActiveTunnels();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error stopping tunnel: {ex.Message}");
            tunnel.Status = TunnelStatus.Error;
            tunnel.LastError = ex.Message;
        }
    }

    /// <summary>
    /// Restarts a tunnel (stop and start again to generate new URL)
    /// </summary>
    public async Task RestartTunnelAsync(CloudflareTunnel tunnel)
    {
        tunnel.Status = TunnelStatus.Stopping;

        try
        {
            // Stop the tunnel
            await _tunnelService.StopTunnelAsync(tunnel.Id);

            Application.Current.Dispatcher.Invoke(() =>
            {
                Tunnels.Remove(tunnel);
                OnPropertyChanged(nameof(ActiveTunnelCount));
            });

            // Wait a moment for cleanup
            await Task.Delay(500);

            // Start a new tunnel for the same port
            await StartTunnelAsync(tunnel.Port);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error restarting tunnel: {ex.Message}");
            tunnel.Status = TunnelStatus.Error;
            tunnel.LastError = ex.Message;
        }
    }

    /// <summary>
    /// Stops all active tunnels
    /// </summary>
    public async Task StopAllTunnelsAsync()
    {
        var tunnelsToStop = Tunnels.ToList();
        
        foreach (var tunnel in tunnelsToStop)
        {
            tunnel.Status = TunnelStatus.Stopping;
        }
        
        await _tunnelService.StopAllTunnelsAsync();
        
        Application.Current.Dispatcher.Invoke(() =>
        {
            Tunnels.Clear();
            OnPropertyChanged(nameof(ActiveTunnelCount));
        });
        
        // Clear saved tunnels from settings
        _settingsService.SaveActiveTunnels(new List<CloudflareTunnel>());
    }

    /// <summary>
    /// Saves active tunnels to settings
    /// </summary>
    private void SaveActiveTunnels()
    {
        var activeTunnels = Tunnels.Where(t => t.Status == TunnelStatus.Active || t.Status == TunnelStatus.Starting).ToList();
        _settingsService.SaveActiveTunnels(activeTunnels);
    }

    /// <summary>
    /// Copies tunnel URL to clipboard
    /// </summary>
    public void CopyUrlToClipboard(string url)
    {
        try
        {
            Clipboard.SetText(url);
            _notificationService.Notify("已复制", "隧道 URL 已复制到剪贴板");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to copy to clipboard: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens tunnel URL in default browser
    /// </summary>
    public void OpenUrlInBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"无法打开 URL：\n{ex.Message}",
                "错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Re-checks cloudflared installation status
    /// </summary>
    public async Task RecheckInstallationAsync()
    {
        IsRefreshing = true;
        OnPropertyChanged(nameof(IsRefreshing));
        await Task.Delay(500); // Longer delay to show loading state
        IsCloudflaredInstalled = _tunnelService.IsCloudflaredInstalled;
        IsRefreshing = false;
        OnPropertyChanged(nameof(IsRefreshing));
    }

    /// <summary>
    /// Re-checks cloudflared installation status (legacy method for compatibility)
    /// </summary>
    public async void RecheckInstallation()
    {
        await RecheckInstallationAsync();
    }

    /// <summary>
    /// Updates uptime for all active tunnels
    /// </summary>
    private void UpdateUptimes()
    {
        foreach (var tunnel in Tunnels.Where(t => t.Status == TunnelStatus.Active))
        {
            // Trigger property change for uptime
            tunnel.OnPropertyChanged(nameof(tunnel.Uptime));
        }
    }

    /// <summary>
    /// Shortens a trycloudflare.com URL for display
    /// </summary>
    private string ShortenUrl(string url)
    {
        return url.Replace("https://", "");
    }

    // INotifyPropertyChanged implementation
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
