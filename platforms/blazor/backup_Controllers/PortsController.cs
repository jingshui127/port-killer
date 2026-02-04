using Microsoft.AspNetCore.Mvc;
using PortKiller.Blazor.Services;
using PortKiller.Blazor.Models;

namespace PortKiller.Blazor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PortsController : ControllerBase
{
    private readonly PortScannerService _portScanner;

    public PortsController(PortScannerService portScanner)
    {
        _portScanner = portScanner;
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
}
