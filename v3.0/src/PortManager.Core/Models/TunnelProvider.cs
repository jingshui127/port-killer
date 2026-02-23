namespace PortManager.Models;

public enum TunnelProvider
{
    Cloudflare,
    LocalTunnel
}

public static class TunnelProviderExtensions
{
    public static string GetDisplayName(this TunnelProvider provider)
    {
        return provider switch
        {
            TunnelProvider.Cloudflare => "Cloudflare",
            TunnelProvider.LocalTunnel => "LocalTunnel",
            _ => "Unknown"
        };
    }

    public static string GetDescription(this TunnelProvider provider)
    {
        return provider switch
        {
            TunnelProvider.Cloudflare => "Cloudflare Quick Tunnels (trycloudflare.com)",
            TunnelProvider.LocalTunnel => "LocalTunnel (localtunnel.me)",
            _ => "Unknown"
        };
    }
}
