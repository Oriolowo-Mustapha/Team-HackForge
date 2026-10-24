using FarmHand.Infrastructure.Services;
using FarmHand.Infrastructure.Persistence;
using FarmHand.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
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
    private readonly FarmHandDbContext _db;
    private readonly IResourceAuthorizationService _authorization;

    public MediaController(LocalImageStorage storage, FarmHandDbContext db, IResourceAuthorizationService authorization)
    {
        _storage = storage;
        _db = db;
        _authorization = authorization;
    }

    [HttpGet("{key}")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get(string key, CancellationToken ct)
    {
        var path = _storage.ResolvePath(key);
        if (path is null) return NotFound();

        var url = $"/api/media/{key}";
        var observation = await _db.PoultryObservations.AsNoTracking()
            .Where(o => o.ImageUrl == url).Select(o => new { o.Id, o.FarmId }).FirstOrDefaultAsync(ct);
        var animal = observation is null ? await _db.LivestockAnimals.AsNoTracking()
            .Where(a => a.PhotoUrl == url).Select(a => new { a.Id, a.FarmId }).FirstOrDefaultAsync(ct) : null;
        var farmId = observation?.FarmId ?? animal?.FarmId;
        if (farmId is null) return NotFound();
        var allowed = await _authorization.CanAccessFarmAsync(farmId.Value, ct);
        if (!allowed && await _authorization.GetCurrentVetAsync(ct) is { } vet)
        {
            Guid? observationId = observation?.Id;
            Guid? animalId = animal?.Id;
            // Acceptance grants case-level access; an open request never grants private-photo access.
            allowed = await (from incident in _db.Incidents
                join request in _db.VetRequests on incident.Id equals request.IncidentId
                where incident.FarmId == farmId.Value && request.VetPartnerId == vet.Id
                    && (request.Status == FarmHand.Domain.Enums.RequestStatus.Responded
                        || request.Status == FarmHand.Domain.Enums.RequestStatus.Completed)
                    && ((observationId != null && incident.SourceObservationId == observationId)
                        || (animalId != null && incident.LivestockAnimalId == animalId))
                select incident.Id).AnyAsync(ct);
        }
        if (!allowed) return NotFound();

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
