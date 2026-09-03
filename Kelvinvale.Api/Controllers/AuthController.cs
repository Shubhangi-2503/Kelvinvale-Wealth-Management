using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Kelvinvale.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    // Public endpoint: Anyone can call this
    [HttpGet("public")]
    public IActionResult PublicEndpoint()
    {
        return Ok(new { message = "Anyone can see this!" });
    }

    // Customer-only endpoint: Requires role 'Customer'
    [HttpGet("customer")]
    [Authorize(Roles = "Adviser")]
    public IActionResult CustomerEndpoint()
    {
        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Ok(new { message = "Hello Customer!", yourId = callerId });
    }

    // Adviser-only endpoint: Requires role 'Adviser'
    [HttpPost("customer")]
    [Authorize(Roles = "Adviser")]
    public IActionResult AdviserEndpoint()
    {
        var callerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Ok(new { message = "Hello Adviser!", yourId = callerId });
    }
}