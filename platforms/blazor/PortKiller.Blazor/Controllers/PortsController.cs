using Microsoft.AspNetCore.Mvc;
using PortKiller.Blazor.Services;
using PortKiller.Blazor.Models;
using System.Text.Json;

namespace PortKiller.Blazor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortsController : ControllerBase
{
    private readonly PortScannerService _portScanner;
    private readonly TunnelService _tunnelService;

    public PortsController(PortScannerService portScanner, TunnelService tunnelService)
    {
        _portScanner = portScanner;
        _tunnelService = tunnelService;
    }

    [HttpGet]
    [Route("ports")]
    public ActionResult<List<PortInfo>> GetPorts()
    {
        return Ok(_portScanner.GetPorts());
    }

    [HttpPost]
    [Route("refresh")]
    public async Task<ActionResult> RefreshPorts()
    {
        await _portScanner.RefreshPortsAsync();
        return Ok(new { success = true, message = "Ports refreshed" });
    }

    [HttpPost]
    [Route("kill/{port}")]
    public ActionResult KillPort(int port)
    {
        _portScanner.KillPort(port);
        return Ok(new { success = true, message = $"Port {port} killed" });
    }

    [HttpPost]
    [Route("favorite/{port}")]
    public ActionResult ToggleFavorite(int port)
    {
        _portScanner.ToggleFavorite(port);
        return Ok(new { success = true, message = $"Port {port} favorite toggled" });
    }

    [HttpPost]
    [Route("watch/{port}")]
    public ActionResult ToggleWatch(int port)
    {
        _portScanner.ToggleWatch(port);
        return Ok(new { success = true, message = $"Port {port} watch toggled" });
    }

    [HttpGet]
    [Route("tunnels")]
    public ActionResult<List<CloudflareTunnel>> GetTunnels()
    {
        _tunnelService.UpdateTunnelStatus();
        return Ok(_tunnelService.GetTunnels());
    }

    [HttpPost]
    [Route("tunnels/create")]
    public async Task<ActionResult<CloudflareTunnel>> CreateTunnel([FromBody] CreateTunnelRequest request)
    {
        try
        {
            var tunnel = await _tunnelService.CreateTunnelAsync(request.Port, request.TunnelName);
            return Ok(tunnel);
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [Route("tunnels/stop/{port}")]
    public ActionResult StopTunnel(int port)
    {
        try
        {
            _tunnelService.StopTunnel(port);
            return Ok(new { success = true, message = $"Tunnel for port {port} stopped" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [Route("tunnels/stop-all")]
    public ActionResult StopAllTunnels()
    {
        try
        {
            _tunnelService.StopAllTunnels();
            return Ok(new { success = true, message = "All tunnels stopped" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [Route("tunnels/restart/{port}")]
    public async Task<ActionResult<CloudflareTunnel>> RestartTunnel(int port)
    {
        try
        {
            var tunnel = await _tunnelService.RestartTunnelAsync(port);
            return Ok(tunnel);
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    [Route("tunnels/cloudflared/status")]
    public async Task<ActionResult> CheckCloudflared()
    {
        var status = await _tunnelService.GetCloudflaredStatusWithUpdateCheckAsync();
        return Ok(status);
    }

    [HttpPost]
    [Route("tunnels/cloudflared/update")]
    public async Task<ActionResult> UpdateCloudflared()
    {
        try
        {
            var newVersion = await _tunnelService.UpdateCloudflaredAsync();
            return Ok(new { success = true, message = "Cloudflared updated successfully", version = newVersion });
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    [Route("tunnels/cloudflared/update/progress")]
    public async Task GetUpdateProgress(CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var tcs = new TaskCompletionSource<bool>();

        EventHandler<CloudflaredUpdateProgress>? handler = null;
        handler = (sender, progress) =>
        {
            try
            {
                var json = JsonSerializer.Serialize(progress);
                var sseMessage = $"data: {json}\n\n";
                Response.WriteAsync(sseMessage, cancellationToken).GetAwaiter().GetResult();
                
                if (progress.IsComplete)
                {
                    tcs.TrySetResult(true);
                }
            }
            catch
            {
                tcs.TrySetResult(false);
            }
        };

        _tunnelService.UpdateProgressChanged += handler;

        try
        {
            await tcs.Task.WaitAsync(TimeSpan.FromMinutes(3), cancellationToken);
        }
        catch (TimeoutException)
        {
        }
        finally
        {
            _tunnelService.UpdateProgressChanged -= handler;
        }
    }

    [HttpGet]
    [Route("admin/check")]
    public ActionResult CheckAdmin()
    {
        var isAdmin = TunnelService.IsRunningAsAdministrator();
        return Ok(new { isAdmin, refreshInterval = 1000 });
    }
}

public class CreateTunnelRequest
{
    public int Port { get; set; }
    public string? TunnelName { get; set; }
}
