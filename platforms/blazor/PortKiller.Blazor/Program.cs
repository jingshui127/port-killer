using PortKiller.Blazor.Data;
using PortKiller.Blazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMasaBlazor();
builder.Services.AddHttpClient();
builder.Services.AddControllers();
builder.Services.AddSingleton<WeatherForecastService>();
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<NotificationService>();
builder.Services.AddSingleton<PortScannerService>();
builder.Services.AddSingleton<TunnelService>();
builder.Services.AddScoped<ThemeService>();

var app = builder.Build();

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
var tunnelService = app.Services.GetRequiredService<TunnelService>();

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

app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
