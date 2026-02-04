using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using PortKiller.Models;
using PortKiller.ViewModels;
using PortKiller.Helpers;

namespace PortKiller;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly TunnelViewModel _tunnelViewModel;
    private Hardcodet.Wpf.TaskbarNotification.TaskbarIcon? _trayIcon;
    private bool _isShuttingDown = false;
    private StackPanel? _loadingState;
    private StackPanel? _tunnelsLoadingState;
    private TextBlock? _tunnelsLoadingText;
    private TextBlock? _tunnelsLoadingSubText;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = App.Services.GetRequiredService<MainViewModel>();
        _tunnelViewModel = App.Services.GetRequiredService<TunnelViewModel>();
        
        // Ensure window is visible and activated on startup
        Loaded += (s, e) =>
        {
            // Get reference to loading state after window is loaded
            _loadingState = this.FindName("LoadingState") as StackPanel;
            
            // Get reference to tunnels loading state after window is loaded
            _tunnelsLoadingState = this.FindName("TunnelsLoadingState") as StackPanel;
            
            // Get reference to tunnels loading text elements
            _tunnelsLoadingText = this.FindName("TunnelsLoadingText") as TextBlock;
            _tunnelsLoadingSubText = this.FindName("TunnelsLoadingSubText") as TextBlock;
            
            InitializeAsync();
        };
        
        // Setup keyboard shortcuts
        SetupKeyboardShortcuts();
        
        // Initialize system tray icon
        InitializeTrayIcon();
        
        Show();
        Activate();
        WindowState = WindowState.Normal;
    }

    private void InitializeTrayIcon()
    {
        _trayIcon = new Hardcodet.Wpf.TaskbarNotification.TaskbarIcon
        {
            ToolTipText = "PortKiller",
            Visibility = Visibility.Visible
        };

        // Create icon from text (simple fallback)
        _trayIcon.Icon = CreateTrayIcon();

        // Setup context menu with dark theme
        var contextMenu = new ContextMenu
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 224, 224)),
            BorderThickness = new Thickness(1),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 60))
        };

        contextMenu.Items.Add(CreateMenuItem("打开主窗口", "🪟", TrayOpenMain_Click, fontWeight: FontWeights.SemiBold));
        contextMenu.Items.Add(new Separator { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 60)) });

        contextMenu.Items.Add(CreateMenuItem("刷新", "↻", TrayRefresh_Click, "Ctrl+R"));
        contextMenu.Items.Add(CreateMenuItem("终止所有进程", "⚡", TrayKillAll_Click, "Ctrl+K"));

        contextMenu.Items.Add(new Separator { Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 60)) });

        contextMenu.Items.Add(CreateMenuItem("设置", "⚙", TraySettings_Click));
        contextMenu.Items.Add(CreateMenuItem("退出", "✕", TrayQuit_Click, "Ctrl+Q"));

        _trayIcon.ContextMenu = contextMenu;
        _trayIcon.TrayLeftMouseDown += TrayIcon_Click;
    }

    private MenuItem CreateMenuItem(string header, string icon, RoutedEventHandler onClick, string? gesture = null, FontWeight? fontWeight = null)
    {
        var item = new MenuItem
        {
            Header = header,
            InputGestureText = gesture,
            FontWeight = fontWeight ?? FontWeights.Normal,
            Icon = new TextBlock 
            { 
                Text = icon, 
                FontSize = 14, 
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0)
            }
        };
        item.Click += onClick;
        return item;
    }

    private System.Drawing.Icon CreateTrayIcon()
    {
        // Create a simple icon with a network symbol
        var bitmap = new System.Drawing.Bitmap(16, 16);
        using (var g = System.Drawing.Graphics.FromImage(bitmap))
        {
            g.Clear(System.Drawing.Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            
            // Draw a simple network/port icon (circle with dot)
            using (var pen = new System.Drawing.Pen(System.Drawing.Color.White, 2))
            {
                g.DrawEllipse(pen, 3, 3, 10, 10);
                g.FillEllipse(System.Drawing.Brushes.White, 6, 6, 4, 4);
            }
        }
        
        return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
    }

    private async void InitializeAsync()
    {
        // Show loading state
        ShowLoadingState();

        await _viewModel.InitializeAsync();
        await _tunnelViewModel.InitializeAsync();
        
        // Subscribe to property changes BEFORE updating UI
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.FilteredPorts) ||
                e.PropertyName == nameof(_viewModel.IsScanning))
            {
                Dispatcher.Invoke(UpdateUI);
            }
            
            if (e.PropertyName == nameof(_viewModel.Ports))
            {
                Dispatcher.Invoke(UpdateTrayMenu);
            }
        };
        
        // Subscribe to tunnel view model property changes
        _tunnelViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_tunnelViewModel.Tunnels) ||
                e.PropertyName == nameof(_tunnelViewModel.IsInitializing) ||
                e.PropertyName == nameof(_tunnelViewModel.IsRefreshing) ||
                e.PropertyName == nameof(_tunnelViewModel.ActiveTunnelCount))
            {
                Dispatcher.Invoke(UpdateTunnelsUI);
            }
        };
        
        // Update UI after subscriptions are set up
        UpdateUI();
        UpdateTunnelsUI();
    }

    private void UpdateUI()
    {
        // Update ports list
        PortsListView.ItemsSource = _viewModel.FilteredPorts;

        // Update empty state
        EmptyState.Visibility = _viewModel.FilteredPorts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Hide loading state
        if (_loadingState != null) _loadingState.Visibility = Visibility.Collapsed;

        // Update status
        StatusText.Text = _viewModel.IsScanning
            ? "正在扫描端口..."
            : $"{_viewModel.FilteredPorts.Count} 个端口";
    }

    private void ShowLoadingState()
    {
        // Show loading state
        EmptyState.Visibility = Visibility.Collapsed;
        if (_loadingState != null) _loadingState.Visibility = Visibility.Visible;
        StatusText.Text = "正在扫描端口...";
    }

    // Window Controls
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshPortsCommand.ExecuteAsync(null);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _viewModel.Search(SearchBox.Text);
    }

    private void SidebarButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tag)
        {
            if (Enum.TryParse<SidebarItem>(tag, out var sidebarItem))
            {
                _viewModel.SelectedSidebarItem = sidebarItem;
                HeaderText.Text = sidebarItem.GetTitle();
                
                // Toggle between ports view and tunnels view
                if (sidebarItem == SidebarItem.CloudflareTunnels)
                {
                    PortsPanel.Visibility = Visibility.Collapsed;
                    DetailPanel.Visibility = Visibility.Collapsed;
                    TunnelsPanel.Visibility = Visibility.Visible;
                    
                    // Update tunnels UI immediately
                    UpdateTunnelsUI();
                }
                else
                {
                    TunnelsPanel.Visibility = Visibility.Collapsed;
                    PortsPanel.Visibility = Visibility.Visible;
                }
                
                // Highlight selected button (optional enhancement)
                foreach (var child in ((button.Parent as Panel)?.Children ?? new UIElementCollection(null, null)))
                {
                    if (child is Button btn)
                    {
                        btn.Background = System.Windows.Media.Brushes.Transparent;
                    }
                }
                button.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(25, 52, 152, 219));
            }
        }
    }

    private void PortItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is PortInfo port)
        {
            _viewModel.SelectedPort = port;
            ShowPortDetails(port);
        }
    }

    private void ShowPortDetails(PortInfo port)
    {
        DetailPanel.Visibility = Visibility.Visible;

        DetailPort.Text = port.DisplayPort;
        DetailProcess.Text = port.ProcessName;
        DetailPid.Text = port.Pid.ToString();
        DetailAddress.Text = port.Address;
        DetailUser.Text = port.User;
        DetailCommand.Text = port.Command;

        // Update favorite button
        FavoriteButton.Content = _viewModel.IsFavorite(port.Port)
            ? "⭐ 从收藏中移除"
            : "⭐ 添加到收藏";

        // Update watch button
        WatchButton.Content = _viewModel.IsWatched(port.Port)
            ? "👁 取消监控"
            : "👁 监控端口";
    }

    private async void KillButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is PortInfo port)
        {
            var dialog = new ConfirmDialog(
                $"您确定要终止端口 {port.Port} 上的进程吗？",
                $"进程: {port.ProcessName}\nPID: {port.Pid}\n\n此操作无法撤销。",
                "终止进程")
            {
                Owner = this
            };
            
            dialog.ShowDialog();

            if (dialog.Result)
            {
                await _viewModel.KillProcessCommand.ExecuteAsync(port);
            }
        }
    }

    private void FavoriteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedPort != null)
        {
            _viewModel.ToggleFavoriteCommand.Execute(_viewModel.SelectedPort.Port);
            ShowPortDetails(_viewModel.SelectedPort);
        }
    }

    private void WatchButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedPort != null)
        {
            var port = _viewModel.SelectedPort.Port;

            if (_viewModel.IsWatched(port))
            {
                _viewModel.RemoveWatchedPortCommand.Execute(port);
            }
            else
            {
                _viewModel.AddWatchedPortCommand.Execute(port);
            }

            ShowPortDetails(_viewModel.SelectedPort);
        }
    }

    private async void ShareTunnelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedPort == null)
            return;

        var port = _viewModel.SelectedPort.Port;

        // Navigate to Cloudflare Tunnels panel in sidebar
        _viewModel.SelectedSidebarItem = SidebarItem.CloudflareTunnels;
        HeaderText.Text = SidebarItem.CloudflareTunnels.GetTitle();
        
        // Toggle to tunnels view
        PortsPanel.Visibility = Visibility.Collapsed;
        DetailPanel.Visibility = Visibility.Collapsed;
        TunnelsPanel.Visibility = Visibility.Visible;
        UpdateTunnelsUI();

        // Start tunnel for the selected port
        await _tunnelViewModel.StartTunnelAsync(port);
    }

    // Window loaded event - enable blur for sidebar only
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Enable acrylic blur effect for the entire window (sidebar will show blur through transparency)
            WindowBlurHelper.EnableAcrylicBlur(this, blurOpacity: 180, blurColor: 0x1A1A1A);
        }
        catch (Exception ex)
        {
            // Blur not supported on this system
            System.Diagnostics.Debug.WriteLine($"Blur effect not supported: {ex.Message}");
        }
    }

    // Keyboard shortcuts
    private void SetupKeyboardShortcuts()
    {
        var refreshGesture = new KeyGesture(Key.R, ModifierKeys.Control);
        var killAllGesture = new KeyGesture(Key.K, ModifierKeys.Control);
        var quitGesture = new KeyGesture(Key.Q, ModifierKeys.Control);

        InputBindings.Add(new KeyBinding(_viewModel.RefreshPortsCommand, refreshGesture));
        InputBindings.Add(new KeyBinding(ApplicationCommands.Close, quitGesture));
        
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Close, (s, e) => Close()));
    }

    // System tray icon handlers
    private void TrayIcon_Click(object sender, RoutedEventArgs e)
    {
        // Show mini popup window near tray
        var miniWindow = new MiniPortKillerWindow();
        miniWindow.ShowNearTray();
    }

    private async void TrayRefresh_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.RefreshPortsCommand.ExecuteAsync(null);
    }

    private async void TrayKillAll_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ConfirmDialog(
            "您确定要终止所有监听端口的进程吗？",
            $"这将终止 {_viewModel.Ports.Count} 个进程。\n\n此操作无法撤销。",
            "终止所有进程")
        {
            Owner = this
        };
        
        dialog.ShowDialog();

        if (dialog.Result)
        {
            foreach (var port in _viewModel.Ports.ToList())
            {
                try
                {
                    await _viewModel.KillProcessCommand.ExecuteAsync(port);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to kill process on port {port.Port}: {ex.Message}");
                }
            }
        }
    }

    // Cloudflare Tunnels Methods
    private void UpdateTunnelsUI()
    {
        System.Diagnostics.Debug.WriteLine($"[UpdateTunnelsUI] Called. IsInitializing={_tunnelViewModel.IsInitializing}, IsRefreshing={_tunnelViewModel.IsRefreshing}, TunnelsCount={_tunnelViewModel.Tunnels.Count}");
        
        // Bind tunnels list to view model
        TunnelsListView.ItemsSource = _tunnelViewModel.Tunnels;
        
        // Handle loading and refreshing states
        if (_tunnelViewModel.IsInitializing || _tunnelViewModel.IsRefreshing)
        {
            if (_tunnelsLoadingState != null)
            {
                _tunnelsLoadingState.Visibility = Visibility.Visible;
                System.Diagnostics.Debug.WriteLine($"[UpdateTunnelsUI] Loading state set to Visible");
            }
            TunnelsListView.Visibility = Visibility.Collapsed;
            TunnelsEmptyState.Visibility = Visibility.Collapsed;
            
            // Update loading text based on state
            if (_tunnelsLoadingText != null)
            {
                _tunnelsLoadingText.Text = _tunnelViewModel.IsInitializing ? "正在加载..." : "正在刷新...";
            }
            if (_tunnelsLoadingSubText != null)
            {
                _tunnelsLoadingSubText.Text = _tunnelViewModel.IsInitializing ? "正在检查隧道状态" : "正在检查 cloudflared 安装状态";
            }
            
            // Don't hide loading state if we're in the middle of a refresh
            return;
        }
        
        // Hide loading state
        if (_tunnelsLoadingState != null)
        {
            _tunnelsLoadingState.Visibility = Visibility.Collapsed;
            System.Diagnostics.Debug.WriteLine($"[UpdateTunnelsUI] Loading state set to Collapsed");
        }
        
        // Show tunnels list
        TunnelsListView.Visibility = Visibility.Visible;
        
        // Update empty state
        TunnelsEmptyState.Visibility = _tunnelViewModel.Tunnels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        
        // Update status bar - tunnel count
        var count = _tunnelViewModel.ActiveTunnelCount;
        TunnelStatusText.Text = $"{count} 个活动隧道";
        TunnelStatusDot.Fill = count > 0 
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 204, 113))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 128, 128));
        
        // Update status bar - cloudflared installation status
        if (_tunnelViewModel.IsCloudflaredInstalled)
        {
            CloudflaredStatusDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 204, 113));
            CloudflaredStatusText.Text = "已安装 cloudflared";
        }
        else
        {
            CloudflaredStatusDot.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60));
            CloudflaredStatusText.Text = "未安装 cloudflared";
        }
        
        // Update Stop All button visibility
        StopAllTunnelsButton.Visibility = _tunnelViewModel.Tunnels.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        
        // Update cloudflared warning
        CloudflaredWarning.Visibility = _tunnelViewModel.IsCloudflaredInstalled ? Visibility.Collapsed : Visibility.Visible;
        
        // Subscribe to collection changes if not already
        _tunnelViewModel.Tunnels.CollectionChanged -= OnTunnelsCollectionChanged;
        _tunnelViewModel.Tunnels.CollectionChanged += OnTunnelsCollectionChanged;
    }

    private void OnTunnelsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            // Don't update UI if still initializing
            if (_tunnelViewModel.IsInitializing)
            {
                return;
            }
            
            TunnelsEmptyState.Visibility = _tunnelViewModel.Tunnels.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            
            var count = _tunnelViewModel.ActiveTunnelCount;
            TunnelStatusText.Text = $"{count} 个活动隧道";
            TunnelStatusDot.Fill = count > 0 
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 204, 113))
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(128, 128, 128));
            
            StopAllTunnelsButton.Visibility = _tunnelViewModel.Tunnels.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private async void RefreshTunnels_Click(object sender, RoutedEventArgs e)
    {
        // Manually show loading state first
        if (_tunnelsLoadingState != null)
        {
            _tunnelsLoadingState.Visibility = Visibility.Visible;
            System.Diagnostics.Debug.WriteLine($"[Refresh] Loading state set to Visible");
        }
        TunnelsListView.Visibility = Visibility.Collapsed;
        TunnelsEmptyState.Visibility = Visibility.Collapsed;
        
        // Update loading text
        if (_tunnelsLoadingText != null)
        {
            _tunnelsLoadingText.Text = "正在刷新...";
        }
        if (_tunnelsLoadingSubText != null)
        {
            _tunnelsLoadingSubText.Text = "正在检查 cloudflared 安装状态";
        }
        
        // Wait for refresh to complete
        await _tunnelViewModel.RecheckInstallationAsync();
        
        // Update UI after refresh completes
        UpdateTunnelsUI();
        System.Diagnostics.Debug.WriteLine($"[Refresh] Refresh completed, IsRefreshing={_tunnelViewModel.IsRefreshing}");
    }

    private async void StopAllTunnels_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ConfirmDialog(
            $"您确定要停止所有 {_tunnelViewModel.Tunnels.Count} 个隧道吗？",
            "所有公共 URL 将立即失效。\n\n此操作无法撤销。",
            "停止所有隧道")
        {
            Owner = this
        };
        
        dialog.ShowDialog();

        if (dialog.Result)
        {
            await _tunnelViewModel.StopAllTunnelsAsync();
            UpdateTunnelsUI();
        }
    }

    private async void TunnelRestart_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: CloudflareTunnel tunnel })
            return;

        await _tunnelViewModel.RestartTunnelAsync(tunnel);
        UpdateTunnelsUI();
    }

    private async void TunnelStop_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: CloudflareTunnel tunnel })
            return;

        await _tunnelViewModel.StopTunnelAsync(tunnel);
    }

    private void TunnelCopy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: CloudflareTunnel tunnel })
            return;

        if (!string.IsNullOrEmpty(tunnel.TunnelUrl))
        {
            _tunnelViewModel.CopyUrlToClipboard(tunnel.TunnelUrl);
        }
    }

    private void TunnelOpen_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: CloudflareTunnel tunnel })
            return;

        if (!string.IsNullOrEmpty(tunnel.TunnelUrl))
        {
            _tunnelViewModel.OpenUrlInBrowser(tunnel.TunnelUrl);
        }
    }

    private void TrayOpenMain_Click(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void TraySettings_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectedSidebarItem = SidebarItem.Settings;
        HeaderText.Text = "设置";
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void TrayQuit_Click(object sender, RoutedEventArgs e)
    {
        _isShuttingDown = true;
        Application.Current.Shutdown();
    }

    // Override close button to minimize to tray instead
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isShuttingDown)
        {
            e.Cancel = true;
            Hide();
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _trayIcon?.Dispose();
        base.OnClosed(e);
    }

    // Update tray menu with active ports
    private void UpdateTrayMenu()
    {
        if (_trayIcon == null || _trayIcon.ContextMenu == null) return;

        var contextMenu = _trayIcon.ContextMenu;
        
        // Remove old port menu items (everything before first separator)
        var firstSeparatorIndex = contextMenu.Items.Cast<object>()
            .TakeWhile(item => item is not Separator)
            .Count();
        
        // Remove old port items
        for (int i = contextMenu.Items.Count - 1; i >= 0; i--)
        {
            if (contextMenu.Items[i] is MenuItem menuItem && menuItem.Tag is PortInfo)
            {
                contextMenu.Items.RemoveAt(i);
            }
        }

        // Add header if there are ports
        var ports = _viewModel.Ports.Take(10).ToList(); // Limit to 10 ports
        
        if (ports.Any())
        {
            // Find the first separator and insert ports before it
            var separatorIndex = -1;
            for (int i = 0; i < contextMenu.Items.Count; i++)
            {
                if (contextMenu.Items[i] is Separator)
                {
                    separatorIndex = i;
                    break;
                }
            }

            if (separatorIndex > 0)
            {
                // Insert ports after "Active Ports" header
                int insertIndex = 1;
                foreach (var port in ports)
                {
                    var portMenuItem = new MenuItem
                    {
                        Header = $":{port.Port}  {port.ProcessName} (PID: {port.Pid})",
                        Icon = new TextBlock { Text = "●", Foreground = System.Windows.Media.Brushes.Green, FontSize = 10, VerticalAlignment = VerticalAlignment.Center },
                        Tag = port
                    };
                    portMenuItem.Click += async (s, e) =>
                    {
                        var menuItem = s as MenuItem;
                        if (menuItem?.Tag is PortInfo portInfo)
                        {
                            var dialog = new ConfirmDialog(
                                $"Kill process on port {portInfo.Port}?",
                                $"Process: {portInfo.ProcessName}\nPID: {portInfo.Pid}",
                                "Kill Process")
                            {
                                Owner = this
                            };
                            
                            dialog.ShowDialog();

                            if (dialog.Result)
                            {
                                await _viewModel.KillProcessCommand.ExecuteAsync(portInfo);
                            }
                        }
                    };
                    contextMenu.Items.Insert(insertIndex++, portMenuItem);
                }
            }
        }
    }
}
