using PortManager.Services;
using PortManager.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMasaBlazor();

// Add HttpClient
builder.Services.AddHttpClient();

// Add PortManager services
builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<NotificationService>();
builder.Services.AddSingleton<PortScannerService>();
builder.Services.AddSingleton<FirewallService>();
builder.Services.AddSingleton<TunnelService>();
builder.Services.AddScoped<ThemeService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Initialize tunnel service
var tunnelService = app.Services.GetRequiredService<TunnelService>();
await tunnelService.InitializeAsync();

app.Run();
