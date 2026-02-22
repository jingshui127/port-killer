using NewLife.Log;
using PortManager.Services;

// 配置 NewLife XTrace 日志
XTrace.UseConsole();

var builder = WebApplication.CreateBuilder(args);

// 添加星尘服务
builder.Services.AddStardust("http://47.113.219.65:6600", "PortManager", null);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMasaBlazor();
builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<NotificationService>();
builder.Services.AddSingleton<FirewallService>();
builder.Services.AddSingleton<PortScannerService>();
builder.Services.AddSingleton<TunnelService>();
builder.Services.AddScoped<ThemeService>();

var app = builder.Build();

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
var tunnelService = app.Services.GetRequiredService<TunnelService>();

// 初始化隧道服务，加载保存的隧道
await tunnelService.InitializeAsync();

lifetime.ApplicationStopping.Register(() =>
{
    tunnelService.SaveActiveTunnels();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

// 使用星尘服务
app.UseStardust();

app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
