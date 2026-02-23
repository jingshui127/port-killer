using Masa.Blazor;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NewLife.Log;
using PortManager.Models;
using PortManager.Services;
using PortManager.Shared.Services;
using System.Text;

namespace PortManager.Shared.Pages;

public partial class Ports : IDisposable
{
    // 注入服务
    [Inject] private PortScannerService PortScanner { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ThemeService ThemeService { get; set; } = default!;
    [Inject] private TunnelService TunnelService { get; set; } = default!;
    [Inject] private NotificationService NotificationService { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
    [Inject] private FirewallService FirewallService { get; set; } = default!;


    // 表格数据模型
    public class PortTableItem
    {
        public int Port { get; set; }
        public string Protocol { get; set; } = "";
        public string Direction { get; set; } = "";
        public string ProcessName { get; set; } = "";
        public string Description { get; set; } = "";
        public string Command { get; set; } = "";
        public string Address { get; set; } = "";
        public string User { get; set; } = "";
        public int? Pid { get; set; }
        public bool HasTunnel { get; set; }
        public bool IsFavorite { get; set; }
        public bool IsWatched { get; set; }
        public bool IsSystemService { get; set; }
        public PortInfo? PrimaryPort { get; set; }
    }

    private List<PortInfo> _ports = new();
    private List<PortInfo> _filteredPorts = new();
    private List<PortTableItem> _tableItems = new();

    // 表格表头定义
    private List<DataTableHeader<PortTableItem>> _tableHeaders = new()
    {
        new() { Text = "端口", Value = nameof(PortTableItem.Port), Fixed = DataTableFixed.Left, Width = 60, Sortable = true },
        new() { Text = "协议", Value = nameof(PortTableItem.Protocol), Width = 80, Sortable = true },
        new() { Text = "方向", Value = nameof(PortTableItem.Direction), Width = 80 },
        new() { Text = "PID", Value = nameof(PortTableItem.Pid), Width = 60, Sortable = true },
        new() { Text = "进程名", Value = nameof(PortTableItem.ProcessName), Width = 100, Sortable = true },
        new() { Text = "说明", Value = nameof(PortTableItem.Description), Width = 150 },
        new() { Text = "命令", Value = nameof(PortTableItem.Command), Width = 200 },
        new() { Text = "地址", Value = nameof(PortTableItem.Address), Width = 100, Sortable = true },
        new() { Text = "用户", Value = nameof(PortTableItem.User), Width = 80, Sortable = true },
        new() { Text = "操作", Value = "Actions", Fixed = DataTableFixed.Right, Width = 160, Sortable = false }
    };
    private bool _isLoading = false;
    private bool _isRefreshing = false;
    private string _searchText = string.Empty;
    private PortInfo? _selectedPort;
    private bool _showPortDetailsModal = false;
    private bool _showFavoritesOnly = false;
    private bool _showWatchedOnly = false;
    private bool _showSystemServicesOnly = false;
    private bool _showTcpOnly = false;
    private bool _showUdpOnly = false;
    private int? _portRangeStart = null;
    private int? _portRangeEnd = null;
    private bool _showNotification = false;
    private string _notificationMessage = string.Empty;
    private string _notificationType = string.Empty;
    private string _notificationIcon = string.Empty;
    private bool _isTableView = false;
    private bool IsLightTheme => ThemeService.CurrentTheme == "light";
    private string _sortBy = nameof(PortInfo.Port);
    private bool _sortDesc = false;
    private HashSet<int> _selectedPorts = new();
    private bool _showNotificationHistoryModal = false;
    private List<Notification> _notificationHistory = new();
    private System.Timers.Timer? _autoRefreshTimer;
    private System.Timers.Timer? _refreshProgressTimer;
    private DateTime _currentRefreshStartTime;
    private bool _autoRefreshEnabled = true;
    private int _refreshInterval = 30; // 秒，根据实际扫描时间调整
    private int _refreshCount = 0;
    private DateTime _lastRefreshTime = DateTime.MinValue;
    private TimeSpan _lastRefreshDuration = TimeSpan.Zero;
    private readonly TimeSpan _minRefreshInterval = TimeSpan.FromSeconds(10); // 最小刷新间隔，避免刷新过于频繁
    private bool _isDisposed = false; // 防止在 Dispose 后继续执行

    // 隧道提供商选择对话框
    private bool _showTunnelProviderDialog = false;
    private int _pendingTunnelPort = 0;
    private TunnelProvider _selectedTunnelProvider = TunnelProvider.Cloudflare;

    protected override async Task OnInitializedAsync()
    {
        await LoadPortsFromCacheAsync();
        _notificationHistory = NotificationService.GetNotificationHistory();
        StartAutoRefresh();

        // 订阅主题变更事件
        ThemeService.OnThemeChanged += OnThemeChanged;

        // 订阅防火墙规则缓存刷新事件
        FirewallService.CacheRefreshed += OnFirewallCacheRefreshed;
    }

    private async void OnFirewallCacheRefreshed(object? sender, EventArgs e)
    {
        await InvokeAsync(async () =>
        {
            XTrace.Log.Info("Firewall cache refreshed, updating port access directions...");
            // 重新获取端口防火墙信息
            await RefreshPortFirewallInfoAsync();
            StateHasChanged();
        });
    }

    private async Task RefreshPortFirewallInfoAsync()
    {
        try
        {
            var ports = _ports.Select(p => p.Port).Distinct().ToList();
            var firewallInfo = FirewallService.GetPortsFirewallInfo(ports);

            foreach (var port in _ports)
            {
                if (firewallInfo.TryGetValue(port.Port, out var info))
                {
                    port.FirewallInfo = info;
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine($"Error refreshing port firewall info: {ex.Message}");
        }
    }

    private async void OnThemeChanged()
    {
        await InvokeAsync(async () =>
        {
            // 强制刷新 UI
            StateHasChanged();
        });
    }

    private void StartAutoRefresh()
    {
        if (_autoRefreshTimer != null) return;

        _autoRefreshTimer = new System.Timers.Timer(_refreshInterval * 1000);
        _autoRefreshTimer.Elapsed += async (sender, e) =>
        {
            // 检查页面是否已释放
            if (_isDisposed)
            {
                XTrace.Log.Debug("Auto refresh skipped: page is disposed");
                return;
            }

            if (_autoRefreshEnabled && !_isLoading)
            {
                // 检查距离上次刷新是否已经超过最小间隔
                var timeSinceLastRefresh = DateTime.Now - _lastRefreshTime;
                if (timeSinceLastRefresh < _minRefreshInterval)
                {
                    XTrace.Log.Debug($"Auto refresh skipped: last refresh was {timeSinceLastRefresh.TotalSeconds:F1}s ago");
                    return;
                }

                await InvokeAsync(async () =>
                {
                    if (!_isDisposed)
                    {
                        await RefreshPortsWithoutLoading();
                    }
                });
            }
        };
        _autoRefreshTimer.AutoReset = true;
        _autoRefreshTimer.Start();
    }

    private void StopAutoRefresh()
    {
        _autoRefreshTimer?.Stop();
        _autoRefreshTimer?.Dispose();
        _autoRefreshTimer = null;
    }

    public void Dispose()
    {
        _isDisposed = true;

        // 先停止所有定时器，防止新的刷新操作
        StopAutoRefresh();
        StopRefreshProgressTimer();
        _notificationTimer?.Stop();
        _notificationTimer?.Dispose();
        _notificationTimer = null;

        // 取消订阅主题变更事件
        ThemeService.OnThemeChanged -= OnThemeChanged;

        // 取消订阅防火墙规则缓存刷新事件
        FirewallService.CacheRefreshed -= OnFirewallCacheRefreshed;

        // 等待一小段时间，让正在进行的刷新操作完成
        Task.Delay(100).Wait();

        // 释放刷新锁
        try
        {
            _refreshLock?.Dispose();
        }
        catch
        {
            // 忽略释放错误
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JSRuntime.InvokeVoidAsync("eval", $"document.body.className = '{ThemeService.CurrentTheme}';");
        }
        await base.OnAfterRenderAsync(firstRender);
    }

    private async Task LoadPortsFromCacheAsync()
    {
        try
        {
            _ports = PortScanner.GetPorts();

            if (_ports.Count == 0)
            {
                await LoadPortsAsync();
            }
            else
            {
                // 从缓存加载也需要更新刷新时间
                _lastRefreshTime = DateTime.Now;
                _refreshCount++;
                // 应用筛选
                FilterPorts();
                StateHasChanged();
                
                // 获取防火墙规则信息
                await RefreshPortFirewallInfoAsync();
            }
        }
        catch (Exception ex)
        {
            XTrace.Log.Error($"Error loading ports from cache: {ex.Message}");
            await LoadPortsAsync();
        }
    }

    private async Task LoadPortsAsync()
    {
        _isLoading = true;
        var startTime = DateTime.Now;
        _currentRefreshStartTime = startTime;
        StateHasChanged();

        // 启动实时刷新进度定时器
        StartRefreshProgressTimer();

        try
        {
            Console.WriteLine("[LoadPortsAsync] Starting to load ports...");
            await Task.Delay(500);
            Console.WriteLine("[LoadPortsAsync] Calling RefreshPortsAsync...");
            await PortScanner.RefreshPortsAsync();
            Console.WriteLine("[LoadPortsAsync] Getting ports from scanner...");
            _ports = PortScanner.GetPorts();
            Console.WriteLine($"[LoadPortsAsync] Loaded {_ports.Count} ports");

            // 更新刷新统计
            _lastRefreshDuration = DateTime.Now - startTime;
            _lastRefreshTime = DateTime.Now;
            _refreshCount++;

            // 应用筛选
            FilterPorts();
        }
        catch (Exception ex)
        {
            XTrace.Log.Error($"Error loading ports: {ex.Message}");
            ShowNotification("加载端口失败", "error");
        }
        finally
        {
            // 停止实时刷新进度定时器
            StopRefreshProgressTimer();
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task RefreshPorts()
    {
        // 使用增量更新方式刷新，避免界面闪烁，显示刷新状态
        await RefreshPortsWithoutLoading(showRefreshing: true);
    }

    private readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);

    private async Task RefreshPortsWithoutLoading(bool showRefreshing = false)
    {
        // 检查页面是否已释放
        if (_isDisposed)
        {
            XTrace.Log.Debug("Refresh skipped: page is disposed");
            return;
        }

        // 手动刷新时等待锁（最多等待5秒），自动刷新时如果拿不到锁立即跳过
        var lockTimeout = showRefreshing ? 5000 : 0;
        if (!await _refreshLock.WaitAsync(lockTimeout))
        {
            XTrace.Log.Info("Refresh skipped: another refresh is in progress");
            return;
        }

        var refreshStartTime = DateTime.Now;
        _currentRefreshStartTime = refreshStartTime;

        // 启动实时刷新进度定时器
        StartRefreshProgressTimer();

        try
        {
            // 检查页面是否已释放
            if (_isDisposed)
            {
                XTrace.Log.Debug("Refresh skipped: page is disposed after acquiring lock");
                return;
            }

            // 手动刷新时显示刷新状态，自动刷新时不显示
            if (showRefreshing)
            {
                _isRefreshing = true;
                StateHasChanged();
            }

            // 后台刷新，不显示加载状态
            await PortScanner.RefreshPortsAsync();
            var newPorts = PortScanner.GetPorts();

            // 检查页面是否已释放
            if (_isDisposed)
            {
                XTrace.Log.Debug("Refresh skipped: page is disposed during refresh");
                return;
            }

            // 增量更新：保留现有端口对象的引用，只更新数据
            UpdatePortsIncrementally(newPorts);

            // 应用当前筛选
            FilterPorts();

            // 增加刷新计数
            _refreshCount++;

            // 记录刷新耗时
            _lastRefreshDuration = DateTime.Now - refreshStartTime;
            _lastRefreshTime = DateTime.Now;

            StateHasChanged();
        }
        catch (Exception ex)
        {
            XTrace.Log.Error($"Error auto-refreshing ports: {ex.Message}");
        }
        finally
        {
            // 停止实时刷新进度定时器
            StopRefreshProgressTimer();

            if (showRefreshing && !_isDisposed)
            {
                _isRefreshing = false;
                StateHasChanged();
            }

            // 安全释放锁
            try
            {
                _refreshLock.Release();
            }
            catch (ObjectDisposedException)
            {
                // 锁已被释放，忽略
            }
        }
    }

    private void StartRefreshProgressTimer()
    {
        StopRefreshProgressTimer();
        _refreshProgressTimer = new System.Timers.Timer(100); // 每100ms更新一次
        _refreshProgressTimer.Elapsed += (sender, e) =>
        {
            if (!_isDisposed)
            {
                InvokeAsync(() =>
                {
                    StateHasChanged();
                });
            }
        };
        _refreshProgressTimer.AutoReset = true;
        _refreshProgressTimer.Start();
    }

    private void StopRefreshProgressTimer()
    {
        _refreshProgressTimer?.Stop();
        _refreshProgressTimer?.Dispose();
        _refreshProgressTimer = null;
    }

    private void UpdatePortsIncrementally(List<PortInfo> newPorts)
    {
        // 创建新端口的字典（使用端口+协议作为复合键）
        var newPortsDict = newPorts.ToDictionary(p => (p.Port, p.Protocol));

        // 跟踪新增和移除的端口用于通知
        var addedPorts = new List<PortInfo>();
        var removedPorts = new List<PortInfo>();

        // 更新现有端口或添加新端口
        foreach (var newPort in newPorts)
        {
            var existingPort = _ports.FirstOrDefault(p => p.Port == newPort.Port && p.Protocol == newPort.Protocol);
            if (existingPort != null)
            {
                // 更新现有端口的属性，保留收藏和监控状态
                existingPort.ProcessName = newPort.ProcessName;
                existingPort.Pid = newPort.Pid;
                existingPort.Address = newPort.Address;
                existingPort.User = newPort.User;
                existingPort.Command = newPort.Command;
                existingPort.IsActive = newPort.IsActive;
            }
            else
            {
                // 添加新端口
                _ports.Add(newPort);
                addedPorts.Add(newPort);
            }
        }

        // 移除不再存在的端口
        var portsToRemove = _ports.Where(p => !newPortsDict.ContainsKey((p.Port, p.Protocol))).ToList();
        foreach (var port in portsToRemove)
        {
            _ports.Remove(port);
            _selectedPorts.Remove(port.Port);
            removedPorts.Add(port);
        }

        // 发送端口变化通知
        if (addedPorts.Count > 0)
        {
            var uniquePorts = addedPorts.GroupBy(p => p.Port).Select(g => g.Key).ToList();
            var message = uniquePorts.Count == 1
                ? $"端口 {uniquePorts[0]} 已启动 ({addedPorts.First().ProcessName})"
                : $"发现 {uniquePorts.Count} 个新端口: {string.Join(", ", uniquePorts.Take(5))}" + (uniquePorts.Count > 5 ? "..." : "");
            ShowNotification(message, "success");
        }
    }

    private async Task KillPort(int port)
    {
        try
        {
            PortScanner.KillPort(port);
            
            // 增量更新：直接从列表中移除被终止的端口，不重新加载
            var portToRemove = _ports.FirstOrDefault(p => p.Port == port);
            if (portToRemove != null)
            {
                _ports.Remove(portToRemove);
                _selectedPorts.Remove(port);
            }
            
            // 同时更新筛选后的列表
            var filteredPortToRemove = _filteredPorts.FirstOrDefault(p => p.Port == port);
            if (filteredPortToRemove != null)
            {
                _filteredPorts.Remove(filteredPortToRemove);
            }
            
            ShowNotification($"端口 {port} 已终止", "success");
            StateHasChanged();
        }
        catch (UnauthorizedAccessException ex)
        {
            // 权限不足，给用户友好的提示
            XTrace.Log.Warn($"Access denied when killing port {port}: {ex.Message}");
            ShowNotification(ex.Message, "warning");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error($"Error killing port: {ex.Message}");
            ShowNotification($"终止端口失败: {ex.Message}", "error");
        }
    }

    private void ToggleFavorite(int port)
    {
        PortScanner.ToggleFavorite(port);
        var portInfo = _ports.FirstOrDefault(p => p.Port == port);
        if (portInfo != null)
        {
            portInfo.IsFavorite = PortScanner.GetPorts().FirstOrDefault(p => p.Port == port)?.IsFavorite ?? false;
        }
        FilterPorts();
    }

    private void ToggleWatch(int port)
    {
        PortScanner.ToggleWatch(port);
        var portInfo = _ports.FirstOrDefault(p => p.Port == port);
        if (portInfo != null)
        {
            portInfo.IsWatched = PortScanner.GetPorts().FirstOrDefault(p => p.Port == port)?.IsWatched ?? false;
        }
        FilterPorts();
    }

    private void ToggleAutoRefresh()
    {
        _autoRefreshEnabled = !_autoRefreshEnabled;
        if (_autoRefreshEnabled)
        {
            StartAutoRefresh();
            ShowNotification("自动刷新已开启", "success");
        }
        else
        {
            StopAutoRefresh();
            ShowNotification("自动刷新已暂停", "info");
        }
    }

    private void OnSearchInput(ChangeEventArgs e)
    {
        _searchText = e.Value?.ToString() ?? string.Empty;
        FilterPorts();
    }

    private void OnPortRangeInput(ChangeEventArgs e)
    {
        FilterPorts();
    }

    private void ClearPortRange()
    {
        _portRangeStart = null;
        _portRangeEnd = null;
        FilterPorts();
    }

    private void ExportToCsv()
    {
        try
        {
            var csv = new StringBuilder();
            csv.AppendLine("端口,协议,进程名,PID,地址,用户,说明,收藏,监控");

            foreach (var port in _filteredPorts.OrderBy(p => p.Port))
            {
                var description = GetPortDescription(port.Port);
                if (string.IsNullOrEmpty(description))
                {
                    description = IsSystemService(port.ProcessName) ? GetSystemServiceDescription(port.ProcessName) : "";
                }
                csv.AppendLine($"{port.Port},{port.Protocol},{EscapeCsvField(port.ProcessName)},{port.Pid},{port.Address},{EscapeCsvField(port.User)},{EscapeCsvField(description)},{(port.IsFavorite ? "是" : "否")},{(port.IsWatched ? "是" : "否")}");
            }

            var fileName = $"端口列表_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            var filePath = Path.Combine(Path.GetTempPath(), fileName);
            File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);

            // 使用默认程序打开CSV文件
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);

            ShowNotification($"已导出 {_filteredPorts.Count} 个端口到 {fileName}", "success");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error($"Error exporting to CSV: {ex.Message}");
            ShowNotification($"导出失败: {ex.Message}", "error");
        }
    }

    private string EscapeCsvField(string field)
    {
        if (string.IsNullOrEmpty(field))
            return "";
        if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
        {
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }
        return field;
    }

    private void ExportToJson()
    {
        try
        {
            var exportData = _filteredPorts.Select(port => new
            {
                port.Port,
                port.Protocol,
                port.ProcessName,
                port.Pid,
                port.Address,
                port.User,
                Command = port.Command,
                Description = IsSystemService(port.ProcessName) ? GetSystemServiceDescription(port.ProcessName) : "",
                port.IsFavorite,
                port.IsWatched,
                IsSystemService = IsSystemService(port.ProcessName)
            }).ToList();

            var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

            var fileName = $"端口列表_{DateTime.Now:yyyyMMdd_HHmmss}.json";
            var filePath = Path.Combine(Path.GetTempPath(), fileName);
            File.WriteAllText(filePath, json, Encoding.UTF8);

            // 使用默认程序打开JSON文件
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);

            ShowNotification($"已导出 {_filteredPorts.Count} 个端口到 {fileName}", "success");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error($"Error exporting to JSON: {ex.Message}");
            ShowNotification($"导出失败: {ex.Message}", "error");
        }
    }

    private void FilterPorts()
    {
        _filteredPorts = _ports.ToList();

        if (_showFavoritesOnly)
        {
            _filteredPorts = _filteredPorts.Where(p => p.IsFavorite).ToList();
        }

        if (_showWatchedOnly)
        {
            _filteredPorts = _filteredPorts.Where(p => p.IsWatched).ToList();
        }

        if (_showSystemServicesOnly)
        {
            _filteredPorts = _filteredPorts.Where(p => IsSystemService(p.ProcessName)).ToList();
        }

        if (_showTcpOnly)
        {
            _filteredPorts = _filteredPorts.Where(p => p.Protocol == "TCP").ToList();
        }

        if (_showUdpOnly)
        {
            _filteredPorts = _filteredPorts.Where(p => p.Protocol == "UDP").ToList();
        }

        if (_portRangeStart.HasValue || _portRangeEnd.HasValue)
        {
            var start = _portRangeStart ?? 1;
            var end = _portRangeEnd ?? 65535;
            _filteredPorts = _filteredPorts.Where(p => p.Port >= start && p.Port <= end).ToList();
        }

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var searchLower = _searchText.ToLower();
            _filteredPorts = _filteredPorts.Where(p =>
                p.Port.ToString().Contains(searchLower) ||
                p.ProcessName.ToLower().Contains(searchLower) ||
                p.Address.ToLower().Contains(searchLower)).ToList();
        }

        _filteredPorts = _sortBy switch
        {
            nameof(PortInfo.Port) => _sortDesc ? _filteredPorts.OrderByDescending(p => p.Port).ToList() : _filteredPorts.OrderBy(p => p.Port).ToList(),
            nameof(PortInfo.ProcessName) => _sortDesc ? _filteredPorts.OrderByDescending(p => p.ProcessName).ToList() : _filteredPorts.OrderBy(p => p.ProcessName).ToList(),
            nameof(PortInfo.Address) => _sortDesc ? _filteredPorts.OrderByDescending(p => p.Address).ToList() : _filteredPorts.OrderBy(p => p.Address).ToList(),
            nameof(PortInfo.User) => _sortDesc ? _filteredPorts.OrderByDescending(p => p.User).ToList() : _filteredPorts.OrderBy(p => p.User).ToList(),
            nameof(PortInfo.Pid) => _sortDesc ? _filteredPorts.OrderByDescending(p => p.Pid).ToList() : _filteredPorts.OrderBy(p => p.Pid).ToList(),
            _ => _filteredPorts
        };

        // 更新表格数据
        UpdateTableItems();
    }

    private void UpdateTableItems()
    {
        var groupedPorts = _filteredPorts.GroupBy(p => p.Port).OrderBy(g => g.Key);
        _tableItems = groupedPorts.Select(group =>
        {
            var tcpPort = group.FirstOrDefault(p => p.Protocol == "TCP");
            var udpPort = group.FirstOrDefault(p => p.Protocol == "UDP");
            var primaryPort = tcpPort ?? udpPort;
            var hasTcp = tcpPort != null;
            var hasUdp = udpPort != null;
            var hasBoth = hasTcp && hasUdp;

            string description;
            if (primaryPort != null && IsSystemService(primaryPort.ProcessName))
            {
                description = GetSystemServiceDescription(primaryPort.ProcessName);
            }
            else if (primaryPort != null)
            {
                var portDesc = GetPortDescription(primaryPort.Port);
                description = !string.IsNullOrEmpty(portDesc) ? portDesc : "-";
            }
            else
            {
                description = "-";
            }

            return new PortTableItem
            {
                Port = group.Key,
                Protocol = hasBoth ? "TCP+UDP" : (hasTcp ? "TCP" : "UDP"),
                Direction = GetAccessDirectionText(primaryPort?.AccessDirection ?? PortAccessDirection.Unknown),
                ProcessName = primaryPort?.ProcessName ?? "Unknown",
                Description = description,
                Command = primaryPort?.Command ?? "-",
                Address = primaryPort?.Address ?? "-",
                User = primaryPort?.User ?? "-",
                Pid = primaryPort?.Pid > 0 ? primaryPort.Pid : null,
                HasTunnel = TunnelService.HasTunnelForPort(group.Key),
                IsFavorite = primaryPort?.IsFavorite ?? false,
                IsWatched = primaryPort?.IsWatched ?? false,
                IsSystemService = primaryPort != null && IsSystemService(primaryPort.ProcessName),
                PrimaryPort = primaryPort
            };
        }).ToList();
    }

    // Windows 系统服务进程列表及描述
    private static readonly Dictionary<string, string> _systemServiceDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "svchost", "Windows服务主机进程 - 承载多个Windows系统服务" },
        { "services", "服务控制管理器 - 管理Windows系统服务的启动和停止" },
        { "lsass", "本地安全认证子系统 - 处理用户登录和权限验证" },
        { "csrss", "客户端/服务器运行时子系统 - Windows核心系统进程" },
        { "smss", "会话管理器子系统 - 管理Windows会话" },
        { "wininit", "Windows启动应用程序 - 系统启动初始化" },
        { "winlogon", "Windows登录应用程序 - 处理用户登录" },
        { "crss", "客户端/服务器运行时子系统 - Windows核心组件" },
        { "system", "系统进程 - Windows操作系统核心" },
        { "registry", "注册表进程 - Windows注册表管理" },
        { "TermService", "远程桌面服务 - 允许远程桌面连接" },
        { "RpcSs", "RPC服务 - 远程过程调用服务" },
        { "Dhcp", "DHCP客户端 - 自动获取IP地址" },
        { "Dnscache", "DNS客户端 - 域名解析服务" },
        { "NlaSvc", "网络位置感知 - 检测网络连接状态" },
        { "netlogon", "Netlogon服务 - 域登录验证服务" },
        { "LanmanServer", "服务器服务 - 文件和打印机共享" },
        { "LanmanWorkstation", "工作站服务 - 网络连接管理" }
    };

    // Linux 系统服务进程列表
    private static readonly HashSet<string> _linuxSystemProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // 内核和初始化进程
        "systemd", "init", "kthreadd", "kworker", "ksoftirqd", "migration", "watchdog",
        // 系统服务
        "sshd", "cron", "rsyslogd", "syslog-ng", "journald", "dbus-daemon",
        // 网络服务
        "NetworkManager", "networkd", "wpa_supplicant", "dhclient", "dhcpcd",
        // 文件系统
        "udevd", "tmpwatch", "logrotate",
        // 安全相关
        "polkitd", "auditd", "selinux", "apparmor",
        // 容器相关
        "dockerd", "containerd", "containerd-shim",
        // 显示服务
        "Xorg", "X", "gdm", "lightdm", "sddm",
        // 其他系统服务
        "accounts-daemon", "acpid", "atd", "bluetoothd", "cupsd",
        "haveged", "irqbalance", "ModemManager", "rtkit-daemon",
        "smartd", "thermald", "udisksd", "upowerd"
    };

    private bool IsSystemService(string processName)
    {
        if (string.IsNullOrEmpty(processName)) return false;
        
        // 检查 Windows 系统服务
        if (_systemServiceDescriptions.ContainsKey(processName))
            return true;
        
        // 检查 Linux 系统服务
        if (_linuxSystemProcesses.Contains(processName))
            return true;
        
        // Linux: 检查是否是内核线程 (以 [ 开头和 ] 结尾)
        if (processName.StartsWith("[") && processName.EndsWith("]"))
            return true;
        
        return false;
    }

    private string GetSystemServiceDescription(string processName)
    {
        // Windows 系统服务
        if (_systemServiceDescriptions.TryGetValue(processName, out var winDesc))
            return winDesc;
        
        // Linux 系统服务描述
        return GetLinuxSystemServiceDescription(processName);
    }
    
    private string GetLinuxSystemServiceDescription(string processName)
    {
        return processName.ToLowerInvariant() switch
        {
            "systemd" => "系统和服务管理器 - Linux系统初始化和服务管理",
            "init" => "系统初始化进程 - 第一个用户空间进程",
            "sshd" => "SSH守护进程 - 安全远程登录服务",
            "cron" => "定时任务守护进程 - 执行计划任务",
            "rsyslogd" => "系统日志服务 - 记录系统日志",
            "journald" => "系统日志服务 - systemd日志管理",
            "dbus-daemon" => "D-Bus消息总线 - 进程间通信",
            "networkmanager" => "网络管理器 - 管理网络连接",
            "dockerd" => "Docker守护进程 - 容器管理服务",
            "containerd" => "容器运行时 - 容器生命周期管理",
            "polkitd" => "策略工具包守护进程 - 权限管理",
            "udevd" => "设备管理器 - 管理硬件设备",
            "xorg" or "x" => "X窗口系统 - 图形显示服务器",
            "gdm" => "GNOME显示管理器 - 图形登录界面",
            "lightdm" => "LightDM显示管理器 - 图形登录界面",
            "kthreadd" => "内核线程守护进程 - 管理内核线程",
            "kworker" => "内核工作线程 - 处理内核任务",
            _ when processName.StartsWith("[") && processName.EndsWith("]") => "内核线程 - Linux内核内部进程",
            _ => "Linux系统服务"
        };
    }

    // 常见端口用途字典
    private static readonly Dictionary<int, string> _portDescriptions = new Dictionary<int, string>
    {
        // Web服务
        { 80, "HTTP - 网页浏览服务" },
        { 443, "HTTPS - 安全网页浏览" },
        { 8080, "HTTP代理/备用Web服务" },
        { 8443, "HTTPS备用端口" },
        
        // 数据库
        { 3306, "MySQL数据库服务" },
        { 1433, "SQL Server数据库" },
        { 5432, "PostgreSQL数据库" },
        { 27017, "MongoDB数据库" },
        { 6379, "Redis缓存服务" },
        { 9200, "Elasticsearch搜索服务" },
        
        // 远程连接
        { 3389, "远程桌面服务(RDP)" },
        { 22, "SSH安全远程连接" },
        { 23, "Telnet远程连接" },
        { 5900, "VNC远程桌面" },
        
        // 邮件服务
        { 25, "SMTP邮件发送" },
        { 110, "POP3邮件接收" },
        { 143, "IMAP邮件接收" },
        { 465, "SMTPS安全邮件发送" },
        { 587, "SMTP邮件提交" },
        { 993, "IMAPS安全邮件接收" },
        { 995, "POP3S安全邮件接收" },
        
        // 文件传输
        { 21, "FTP文件传输" },
        { 20, "FTP数据传输" },
        { 69, "TFTP简单文件传输" },
        { 445, "SMB文件共享" },
        { 139, "NetBIOS文件共享" },
        
        // 开发工具
        { 3000, "React/Vue/Grafana开发服务器" },
        { 4200, "Angular开发服务器" },
        { 5000, "Flask/Python开发服务器" },
        { 8000, "Django开发服务器" },
        { 9000, "PHP-FPM/Hadoop" },

        // 消息队列
        { 5672, "RabbitMQ消息队列" },
        { 9092, "Kafka消息队列" },
        { 2181, "ZooKeeper协调服务" },

        // 监控和日志
        { 9090, "Prometheus监控" },
        { 5601, "Kibana日志分析" },
        { 5044, "Logstash日志收集" },
        
        // 容器和编排
        { 2375, "Docker守护进程" },
        { 2376, "Docker安全连接" },
        { 6443, "Kubernetes API" },
        { 10250, "Kubelet节点代理" },
        
        // 游戏服务器
        { 25565, "Minecraft服务器" },
        { 27015, "Steam游戏服务器" },
        
        // 其他常用服务
        { 53, "DNS域名解析" },
        { 88, "Kerberos认证" },
        { 389, "LDAP目录服务" },
        { 636, "LDAPS安全目录服务" },
        { 161, "SNMP网络监控" },
        { 162, "SNMP Trap通知" },
        { 1080, "SOCKS代理" },
        { 3128, "Squid代理服务器" },
        { 8140, "Puppet配置管理" },
        { 8600, "Consul服务发现" },
    };

    private string GetPortDescription(int port)
    {
        return _portDescriptions.TryGetValue(port, out var description) 
            ? description 
            : string.Empty;
    }

    private void ToggleFilter(string filter)
    {
        switch (filter)
        {
            case "all":
                _showFavoritesOnly = false;
                _showWatchedOnly = false;
                _showSystemServicesOnly = false;
                _showTcpOnly = false;
                _showUdpOnly = false;
                break;
            case "favorites":
                _showFavoritesOnly = !_showFavoritesOnly;
                _showWatchedOnly = false;
                _showSystemServicesOnly = false;
                _showTcpOnly = false;
                _showUdpOnly = false;
                break;
            case "watched":
                _showWatchedOnly = !_showWatchedOnly;
                _showFavoritesOnly = false;
                _showSystemServicesOnly = false;
                _showTcpOnly = false;
                _showUdpOnly = false;
                break;
            case "system":
                _showSystemServicesOnly = !_showSystemServicesOnly;
                _showFavoritesOnly = false;
                _showWatchedOnly = false;
                _showTcpOnly = false;
                _showUdpOnly = false;
                break;
            case "tcp":
                _showTcpOnly = !_showTcpOnly;
                _showUdpOnly = false;
                _showFavoritesOnly = false;
                _showWatchedOnly = false;
                _showSystemServicesOnly = false;
                break;
            case "udp":
                _showUdpOnly = !_showUdpOnly;
                _showTcpOnly = false;
                _showFavoritesOnly = false;
                _showWatchedOnly = false;
                _showSystemServicesOnly = false;
                break;
        }
        FilterPorts();
    }

    private void ToggleView()
    {
        _isTableView = !_isTableView;
        StateHasChanged();
    }

    private void ToggleSort(string sortBy)
    {
        if (_sortBy == sortBy)
        {
            _sortDesc = !_sortDesc;
        }
        else
        {
            _sortBy = sortBy;
            _sortDesc = false;
        }
        FilterPorts();
    }

    private void TogglePortSelection(int port, object? isChecked)
    {
        if (isChecked is bool selected)
        {
            if (selected)
            {
                _selectedPorts.Add(port);
            }
            else
            {
                _selectedPorts.Remove(port);
            }
        }
    }

    private void TogglePortSelection(int port)
    {
        if (_selectedPorts.Contains(port))
        {
            _selectedPorts.Remove(port);
        }
        else
        {
            _selectedPorts.Add(port);
        }
    }

    private void ClearSelection()
    {
        _selectedPorts.Clear();
    }

    private async Task BatchKillPorts()
    {
        if (_selectedPorts.Count == 0) return;

        var killedCount = 0;
        var accessDeniedCount = 0;
        var errorMessages = new List<string>();
        
        foreach (var port in _selectedPorts.ToList())
        {
            try
            {
                PortScanner.KillPort(port);
                
                // 增量更新：直接从列表中移除被终止的端口
                var portToRemove = _ports.FirstOrDefault(p => p.Port == port);
                if (portToRemove != null)
                {
                    _ports.Remove(portToRemove);
                    killedCount++;
                }
                
                // 同时更新筛选后的列表
                var filteredPortToRemove = _filteredPorts.FirstOrDefault(p => p.Port == port);
                if (filteredPortToRemove != null)
                {
                    _filteredPorts.Remove(filteredPortToRemove);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                accessDeniedCount++;
                if (!errorMessages.Contains(ex.Message))
                {
                    errorMessages.Add(ex.Message);
                }
            }
            catch (Exception ex)
            {
                XTrace.Log.Error($"Error killing port {port}: {ex.Message}");
            }
        }
        
        _selectedPorts.Clear();
        StateHasChanged();
        
        // 显示结果通知
        if (killedCount > 0 && accessDeniedCount > 0)
        {
            ShowNotification($"已终止 {killedCount} 个端口，{accessDeniedCount} 个端口权限不足", "warning");
        }
        else if (killedCount > 0)
        {
            ShowNotification($"已终止 {killedCount} 个端口", "success");
        }
        else if (accessDeniedCount > 0)
        {
            ShowNotification($"{accessDeniedCount} 个端口无法终止，请以管理员身份运行应用程序", "warning");
        }
    }

    private void ShowPortDetails(PortInfo port)
    {
        _selectedPort = port;
        _showPortDetailsModal = true;
        _ = InvokeAsync(() => StateHasChanged());
    }

    private void ClosePortDetailsModal()
    {
        _showPortDetailsModal = false;
        _selectedPort = null;
        StateHasChanged();
    }

    private void CreateTunnelForPort(int port)
    {
        // 显示提供商选择对话框
        _pendingTunnelPort = port;
        _selectedTunnelProvider = TunnelProvider.Cloudflare; // 默认选择 Cloudflare
        _showTunnelProviderDialog = true;
        StateHasChanged();
    }

    private async Task ConfirmCreateTunnel()
    {
        _showTunnelProviderDialog = false;
        var port = _pendingTunnelPort;
        var provider = _selectedTunnelProvider;
        await CreateTunnelWithProviderAsync(port, provider);
    }

    private async Task HandleProviderSelected(TunnelProvider provider)
    {
        _selectedTunnelProvider = provider;
        await ConfirmCreateTunnel();
    }

    private async Task CreateTunnelWithProviderAsync(int port, TunnelProvider provider)
    {
        try
        {
            await TunnelService.CreateTunnelAsync(port, provider: provider);
            var providerName = provider.GetDisplayName();
            ShowNotification($"端口 {port} 的 {providerName} 隧道已创建", "success");
            Navigation.NavigateTo("/tunnels");
        }
        catch (Exception ex)
        {
            XTrace.Log.Error($"Error creating tunnel: {ex.Message}");
            ShowNotification($"创建隧道失败: {ex.Message}", "error");
        }
    }

    private void CancelCreateTunnel()
    {
        _showTunnelProviderDialog = false;
        _pendingTunnelPort = 0;
        StateHasChanged();
    }

    private System.Timers.Timer? _notificationTimer;

    private void ShowNotification(string message, string type = "success")
    {
        _notificationMessage = message;
        _notificationType = type;
        _notificationIcon = type switch
        {
            "success" => "✓",
            "error" => "✕",
            "warning" => "⚠",
            "info" => "ℹ",
            _ => "ℹ"
        };
        _showNotification = true;

        // 同时添加到通知历史
        NotificationService.AddNotification(type, _notificationIcon, message);

        StateHasChanged();

        // 自动隐藏通知
        StartNotificationTimer();
    }

    private void StartNotificationTimer()
    {
        _notificationTimer?.Stop();
        _notificationTimer?.Dispose();
        
        _notificationTimer = new System.Timers.Timer(5000); // 5秒后自动隐藏
        _notificationTimer.Elapsed += (sender, e) =>
        {
            InvokeAsync(() =>
            {
                HideNotification();
            });
        };
        _notificationTimer.AutoReset = false;
        _notificationTimer.Start();
    }

    private void HideNotification()
    {
        _showNotification = false;
        _notificationTimer?.Stop();
        _notificationTimer?.Dispose();
        _notificationTimer = null;
        StateHasChanged();
    }

    private void ShowNotificationHistory()
    {
        _notificationHistory = NotificationService.GetNotificationHistory();
        _showNotificationHistoryModal = true;
        StateHasChanged();
    }

    private void CloseNotificationHistoryModal()
    {
        _showNotificationHistoryModal = false;
        StateHasChanged();
    }

    private void ClearNotificationHistory()
    {
        NotificationService.ClearHistory();
        _notificationHistory.Clear();
        ShowNotification("通知历史已清除", "success");
    }

    private string GetLastRefreshTimeText()
    {
        if (_lastRefreshTime == DateTime.MinValue)
            return "从未刷新";

        return _lastRefreshTime.ToString("HH:mm:ss");
    }

    private string GetRefreshDurationText()
    {
        if (_lastRefreshDuration.TotalSeconds < 1)
            return $"{_lastRefreshDuration.TotalMilliseconds:F0}ms";
        else if (_lastRefreshDuration.TotalSeconds < 60)
            return $"{_lastRefreshDuration.TotalSeconds:F1}s";
        else
            return $"{_lastRefreshDuration.TotalMinutes:F1}m";
    }

    /// <summary>
    /// 获取进程详细信息
    /// </summary>
    private ProcessDetails? GetProcessDetails(int pid)
    {
        if (pid <= 0) return null;

        try
        {
            var process = System.Diagnostics.Process.GetProcessById(pid);
            if (process == null) return null;

            // 获取内存使用量（KB）
            var memoryKB = process.WorkingSet64 / 1024;

            // 获取CPU使用率
            var cpuPercent = GetProcessCpuPercent(process);

            // 获取进程优先级
            var priority = GetPriorityText(process.PriorityClass);

            // 获取启动时间
            var startTime = GetStartTimeText(process.StartTime);

            // 获取进程状态
            var status = GetProcessStatus(process);

            // 获取子进程数量
            var childrenCount = 0;
            try
            {
                var searcher = new System.Management.ManagementObjectSearcher(
                    $"SELECT * FROM Win32_Process WHERE ParentProcessId = {pid}");
                childrenCount = searcher.Get().Count;
            }
            catch { }

            return new ProcessDetails
            {
                MemoryKB = memoryKB,
                CpuPercent = cpuPercent,
                Priority = priority,
                StartTime = startTime,
                ProcessStatus = status,
                ChildrenCount = childrenCount
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获取进程优先级文本
    /// </summary>
    private string GetPriorityText(System.Diagnostics.ProcessPriorityClass priority)
    {
        return priority switch
        {
            System.Diagnostics.ProcessPriorityClass.RealTime => "实时",
            System.Diagnostics.ProcessPriorityClass.High => "高",
            System.Diagnostics.ProcessPriorityClass.AboveNormal => "高于正常",
            System.Diagnostics.ProcessPriorityClass.Normal => "正常",
            System.Diagnostics.ProcessPriorityClass.BelowNormal => "低于正常",
            System.Diagnostics.ProcessPriorityClass.Idle => "低",
            _ => "未知"
        };
    }

    /// <summary>
    /// 获取启动时间文本
    /// </summary>
    private string GetStartTimeText(DateTime startTime)
    {
        var elapsed = DateTime.Now - startTime;
        
        if (elapsed.TotalDays >= 1)
            return $"{elapsed.TotalDays:F0} 天前";
        else if (elapsed.TotalHours >= 1)
            return $"{elapsed.TotalHours:F0} 小时前";
        else if (elapsed.TotalMinutes >= 1)
            return $"{elapsed.TotalMinutes:F0} 分钟前";
        else
            return $"{elapsed.TotalSeconds:F0} 秒前";
    }

    /// <summary>
    /// 获取进程状态
    /// </summary>
    private ProcessStatus GetProcessStatus(System.Diagnostics.Process process)
    {
        try
        {
            if (process.HasExited)
                return ProcessStatus.Exited;
            
            // 检查是否响应
            if (!process.Responding)
                return ProcessStatus.NotResponding;
            
            return ProcessStatus.Running;
        }
        catch
        {
            return ProcessStatus.Unknown;
        }
    }

    /// <summary>
    /// 获取进程CPU使用率
    /// </summary>
    private double GetProcessCpuPercent(System.Diagnostics.Process process)
    {
        try
        {
            // 获取初始CPU时间
            var startTime = DateTime.UtcNow;
            var startCpuUsage = process.TotalProcessorTime;

            // 等待一小段时间
            System.Threading.Thread.Sleep(100);

            // 获取结束CPU时间
            var endTime = DateTime.UtcNow;
            var endCpuUsage = process.TotalProcessorTime;

            // 计算CPU使用率
            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuCount = Environment.ProcessorCount;

            var cpuPercent = cpuUsedMs / (totalMsPassed * cpuCount) * 100;

            return Math.Min(cpuPercent, 100); // 限制在100%以内
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// 打开进程所在文件夹
    /// </summary>
    /// <summary>
    /// 打开进程所在文件夹（异步执行，避免阻塞UI）
    /// </summary>
    private async Task OpenProcessFolder(string command)
    {
        // 立即关闭弹窗，提供即时反馈
        ClosePortDetailsModal();
        
        await Task.Run(() =>
        {
            try
            {
                if (string.IsNullOrEmpty(command)) return;

                var folderPath = Path.GetDirectoryName(command);
                if (string.IsNullOrEmpty(folderPath)) return;

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{command}\"",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                XTrace.Log.Error($"Error opening process folder: {ex.Message}");
                // 使用InvokeAsync确保在主线程上显示通知
                InvokeAsync(() => ShowNotification("打开文件夹失败", "error"));
            }
        });
    }

    /// <summary>
    /// 打开任务管理器（异步执行，避免阻塞UI）
    /// </summary>
    private async Task OpenTaskManager(int pid)
    {
        // 立即关闭弹窗，提供即时反馈
        ClosePortDetailsModal();
        
        await Task.Run(() =>
        {
            try
            {
                // 启动任务管理器
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "taskmgr.exe",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);

                // 使用InvokeAsync确保在主线程上显示通知
                InvokeAsync(() => ShowNotification("任务管理器已打开", "success"));
            }
            catch (Exception ex)
            {
                XTrace.Log.Error($"Error opening task manager: {ex.Message}");
                InvokeAsync(() => ShowNotification("打开任务管理器失败", "error"));
            }
        });
    }

    /// <summary>
    /// 进程状态枚举
    /// </summary>
    private enum ProcessStatus
    {
        Running,
        NotResponding,
        Exited,
        Unknown
    }

    /// <summary>
    /// 进程详情数据类
    /// </summary>
    private class ProcessDetails
    {
        public long MemoryKB { get; set; }
        public double CpuPercent { get; set; }
        public string Priority { get; set; } = "正常";
        public string StartTime { get; set; } = "";
        public ProcessStatus ProcessStatus { get; set; } = ProcessStatus.Unknown;
        public int ChildrenCount { get; set; }
    }

    /// <summary>
    /// 格式化内存显示（自动切换 KB/MB）
    /// </summary>
    private string FormatMemory(long memoryKB)
    {
        if (memoryKB >= 1024)
        {
            return $"{memoryKB / 1024.0:F1} MB";
        }
        else
        {
            return $"{memoryKB} KB";
        }
    }

    /// <summary>
    /// 获取CPU样式类
    /// </summary>
    private string GetCpuClass(double cpuPercent)
    {
        return cpuPercent switch
        {
            >= 80 => "cpu-high",
            >= 50 => "cpu-medium",
            > 0 => "cpu-low",
            _ => ""
        };
    }

    /// <summary>
    /// 获取状态样式类
    /// </summary>
    private string GetStatusClass(ProcessStatus? status)
    {
        return status switch
        {
            ProcessStatus.Running => "status-running",
            ProcessStatus.NotResponding => "status-not-responding",
            ProcessStatus.Exited => "status-exited",
            _ => "status-unknown"
        };
    }

    /// <summary>
    /// 获取状态文本
    /// </summary>
    private string GetStatusText(ProcessStatus? status, bool canKill)
    {
        var statusText = status switch
        {
            ProcessStatus.Running => "● 运行中",
            ProcessStatus.NotResponding => "⚠ 未响应",
            ProcessStatus.Exited => "✕ 已退出",
            _ => "? 未知"
        };

        if (!canKill && status == ProcessStatus.Running)
        {
            statusText += " 🛡️";
        }

        return statusText;
    }

    private string GetCurrentRefreshDurationText()
    {
        // 如果还没有设置开始时间，返回 0ms
        if (_currentRefreshStartTime == DateTime.MinValue)
            return "0ms";

        var elapsed = DateTime.Now - _currentRefreshStartTime;
        if (elapsed.TotalSeconds < 1)
            return $"{elapsed.TotalMilliseconds:F0}ms";
        else if (elapsed.TotalSeconds < 60)
            return $"{elapsed.TotalSeconds:F1}s";
        else
            return $"{elapsed.TotalMinutes:F1}m";
    }

    private string GetAccessDirectionBadge(PortAccessDirection direction)
    {
        return direction switch
        {
            PortAccessDirection.Bidirectional => "<span class=\"access-badge bidirectional\" title=\"允许入站和出站\">🔄 双向</span>",
            PortAccessDirection.Inbound => "<span class=\"access-badge inbound\" title=\"仅允许入站\">📥 入站</span>",
            PortAccessDirection.Outbound => "<span class=\"access-badge outbound\" title=\"仅允许出站\">📤 出站</span>",
            PortAccessDirection.Blocked => "<span class=\"access-badge blocked\" title=\"已阻止\">🚫 阻止</span>",
            _ => "<span class=\"access-badge unknown\" title=\"未知\">❓ 未知</span>"
        };
    }

    private string GetAccessDirectionText(PortAccessDirection direction)
    {
        return direction switch
        {
            PortAccessDirection.Bidirectional => "<span class=\"access-badge bidirectional\" title=\"允许入站和出站\">🔄 双向</span>",
            PortAccessDirection.Inbound => "<span class=\"access-badge inbound\" title=\"仅允许入站\">📥 入站</span>",
            PortAccessDirection.Outbound => "<span class=\"access-badge outbound\" title=\"仅允许出站\">📤 出站</span>",
            PortAccessDirection.Blocked => "<span class=\"access-badge blocked\" title=\"已阻止\">🚫 阻止</span>",
            _ => "<span class=\"access-badge unknown\" title=\"未知\">❓ 未知</span>"
        };
    }

    private string GetAccessDirectionArrow(PortAccessDirection direction)
    {
        return direction switch
        {
            PortAccessDirection.Bidirectional => "<span class=\"access-arrow bidirectional\" title=\"允许入站和出站\">⇄</span>",
            PortAccessDirection.Inbound => "<span class=\"access-arrow inbound\" title=\"仅允许入站\">←</span>",
            PortAccessDirection.Outbound => "<span class=\"access-arrow outbound\" title=\"仅允许出站\">→</span>",
            PortAccessDirection.Blocked => "<span class=\"access-arrow blocked\" title=\"已阻止\">✕</span>",
            _ => "<span class=\"access-arrow unknown\" title=\"未知\">?</span>"
        };
    }

    /// <summary>
    /// 获取端口用途说明
    /// </summary>
    private string GetPortUsageDescription(int port)
    {
        return port switch
        {
            20 => "FTP 数据传输",
            21 => "FTP 控制连接",
            22 => "SSH 安全 shell",
            23 => "Telnet 远程登录",
            25 => "SMTP 邮件发送",
            53 => "DNS 域名解析",
            80 => "HTTP 网页服务",
            110 => "POP3 邮件接收",
            143 => "IMAP 邮件访问",
            443 => "HTTPS 安全网页",
            3306 => "MySQL 数据库",
            3389 => "RDP 远程桌面",
            5432 => "PostgreSQL 数据库",
            6379 => "Redis 缓存",
            8080 => "HTTP 代理/备用",
            8443 => "HTTPS 备用",
            27017 => "MongoDB 数据库",
            _ => $"端口 {port}"
        };
    }

}
