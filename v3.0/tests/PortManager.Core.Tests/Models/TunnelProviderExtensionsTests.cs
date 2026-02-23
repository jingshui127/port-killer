using PortManager.Models;

namespace PortManager.Core.Tests.Models;

public class TunnelProviderExtensionsTests
{
    [Theory]
    [InlineData(TunnelProvider.Cloudflare, "Cloudflare")]
    [InlineData(TunnelProvider.LocalTunnel, "LocalTunnel")]
    public void GetDisplayName_ReturnsCorrectName(TunnelProvider provider, string expected)
    {
        // Act
        var result = provider.GetDisplayName();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetDisplayName_ReturnsUnknown_ForInvalidValue()
    {
        // Arrange
        var invalidProvider = (TunnelProvider)999;

        // Act
        var result = invalidProvider.GetDisplayName();

        // Assert
        Assert.Equal("Unknown", result);
    }

    [Theory]
    [InlineData(TunnelProvider.Cloudflare, "Cloudflare Quick Tunnels (trycloudflare.com)")]
    [InlineData(TunnelProvider.LocalTunnel, "LocalTunnel (localtunnel.me)")]
    public void GetDescription_ReturnsCorrectDescription(TunnelProvider provider, string expected)
    {
        // Act
        var result = provider.GetDescription();

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetDescription_ReturnsUnknown_ForInvalidValue()
    {
        // Arrange
        var invalidProvider = (TunnelProvider)999;

        // Act
        var result = invalidProvider.GetDescription();

        // Assert
        Assert.Equal("Unknown", result);
    }
}
