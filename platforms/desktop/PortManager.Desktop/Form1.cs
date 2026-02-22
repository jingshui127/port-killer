using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using Masa.Blazor;
using PortManager.Services;

namespace PortManager.Desktop;

public partial class Form1 : Form
{
    public Form1()
    {
        InitializeComponent();
        try
        {
            InitializeBlazor();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"初始化失败: {ex.Message}\n\n{ex.StackTrace}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            throw;
        }
    }

    private void InitializeBlazor()
    {
        var services = new ServiceCollection();

        // 添加 MASA Blazor
        services.AddMasaBlazor();

        // 添加 PortManager 服务
        services.AddSingleton<SettingsService>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<PortScannerService>();
        services.AddSingleton<FirewallService>();
        services.AddSingleton<TunnelService>();

        var serviceProvider = services.BuildServiceProvider();

        // 初始化隧道服务
        var tunnelService = serviceProvider.GetRequiredService<TunnelService>();
        tunnelService.InitializeAsync().Wait();

        // 配置 BlazorWebView
        var blazorWebView = new BlazorWebView
        {
            Dock = DockStyle.Fill,
            HostPage = "wwwroot/index.html"
        };

        blazorWebView.Services = serviceProvider;
        blazorWebView.RootComponents.Add<PortManager.App>("#app");

        Controls.Add(blazorWebView);
    }
}
