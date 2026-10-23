using FarmHand.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FarmHand.API.Controllers;

/// <summary>Serves locally stored uploads to signed-in users (used when Cloudinary is not configured).</summary>
[ApiController]
[Route("api/media")]
[Authorize]
public class MediaController : ControllerBase
{
    private readonly LocalImageStorage _storage;

    public MediaController(LocalImageStorage storage)
    {
        _storage = storage;
    }

    [HttpGet("{key}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Client)]
    public IActionResult Get(string key)
    {
        var path = _storage.ResolvePath(key);
        if (path is null) return NotFound();

        var contentType = Path.GetExtension(path) switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".heic" => "image/heic",
            _ => "image/jpeg"
        };
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(path, contentType);
    }
}
