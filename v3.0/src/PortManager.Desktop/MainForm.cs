using Microsoft.AspNetCore.Components.WebView.WindowsForms;
using Microsoft.Extensions.DependencyInjection;
using PortManager.Services;
using PortManager.Shared.Services;

namespace PortManager.Desktop;

public partial class MainForm : Form
{
    public MainForm()
    {
        InitializeComponent();

        // Set window icon
        this.Icon = new System.Drawing.Icon("appicon.ico");

        var services = new ServiceCollection();
        services.AddWindowsFormsBlazorWebView();
        services.AddMasaBlazor();

        // Add HttpClient
        services.AddHttpClient();

        // Add PortManager services
        services.AddSingleton<SettingsService>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<PortScannerService>();
        services.AddSingleton<FirewallService>();
        services.AddSingleton<TunnelService>();
        services.AddScoped<ThemeService>();

#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif

        var serviceProvider = services.BuildServiceProvider();

        // Initialize tunnel service
        var tunnelService = serviceProvider.GetRequiredService<TunnelService>();
        tunnelService.InitializeAsync().Wait();

        blazorWebView1.HostPage = "wwwroot/index.html";
        blazorWebView1.Services = serviceProvider;
        blazorWebView1.RootComponents.Add<App>("#app");
    }
}
