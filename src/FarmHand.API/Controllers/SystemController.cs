using FarmHand.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

/// <summary>Public, non-secret description of how this server is configured, so the UI can label demo and simulated features honestly.</summary>
[ApiController]
[Route("api/system")]
[AllowAnonymous]
public class SystemController : ControllerBase
{
    [HttpGet("info")]
    [ProducesResponseType(typeof(IntegrationStatus), StatusCodes.Status200OK)]
    public IActionResult Info([FromServices] ISystemStatus status) => Ok(status.GetIntegrationStatus());
}
