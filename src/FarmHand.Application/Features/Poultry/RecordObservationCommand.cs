using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Enums;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Poultry;

/// <summary>Records a poultry observation from free text, optionally with an image.</summary>
/// <param name="FlockId">Flock id.</param>
/// <param name="RawText">Farmer's raw text; sent to Gemini with Groq fallback.</param>
/// <param name="IdempotencyKey">Client key; reusing a key returns the existing observation.</param>
/// <param name="ImageUrl">Optional image URL that triggers vision analysis.</param>
/// <param name="PreExtracted">Optional pre-extracted fields to avoid a second AI call.</param>
/// <param name="PreAnalyzedVisionJson">Optional precomputed vision-analysis JSON.</param>
public record RecordObservationCommand(
    Guid FlockId,
    string RawText,
    string IdempotencyKey,
    string? ImageUrl = null,
    ExtractedObservation? PreExtracted = null,
    string? PreAnalyzedVisionJson = null) : IRequest<Guid>;

public class RecordObservationCommandHandler : IRequestHandler<RecordObservationCommand, Guid>
{
    private readonly IFarmAiService _aiService;
    private readonly IVisionAnalysisService _visionService;
    private readonly IPoultryObservationRepository _observationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RecordObservationCommandHandler(
        IFarmAiService aiService,
        IVisionAnalysisService visionService,
        IPoultryObservationRepository observationRepository,
        IUnitOfWork unitOfWork)
    {
        _aiService = aiService;
        _visionService = visionService;
        _observationRepository = observationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> Handle(RecordObservationCommand request, CancellationToken ct)
    {
        // Idempotency: if this key was already used, return existing observation
        if (await _observationRepository.ExistsByIdempotencyKeyAsync(request.IdempotencyKey, ct))
        {
            var existing = await _observationRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey, ct);
            return existing!.Id;
        }

        // Use pre-extracted data if provided (avoids double AI extraction from VoiceController)
        ExtractedObservation extracted;
        if (request.PreExtracted is not null)
        {
            extracted = request.PreExtracted;
        }
        else
        {
            extracted = await _aiService.ExtractObservationAsync(request.RawText, ct);
        }

        // Range validation
        if (extracted.MortalityCount < 0 || extracted.MortalityCount > 10000)
            throw new ArgumentOutOfRangeException(nameof(extracted.MortalityCount), "Mortality count must be between 0 and 10000.");

        string? visionAnalysis = null;
        if (!string.IsNullOrWhiteSpace(request.PreAnalyzedVisionJson))
        {
            visionAnalysis = request.PreAnalyzedVisionJson;
        }
        else if (!string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            var vision = await _visionService.AnalyzeImageAsync(request.ImageUrl, extracted.FarmerNotes ?? request.RawText, ct);
            visionAnalysis = System.Text.Json.JsonSerializer.Serialize(new
            {
                vision.Description,
                vision.DetectedIssues,
                vision.Confidence,
                AnalyzedAt = DateTime.UtcNow
            });
        }

        var observation = new PoultryObservation(
            request.FlockId,
            DateTime.UtcNow,
            extracted.MortalityCount,
            extracted.WaterConsumption,
            extracted.FeedConsumption,
            extracted.EggProduction,
            extracted.FarmerNotes,
            request.ImageUrl,
            visionAnalysis);

        observation.MarkAiExtracted(request.PreExtracted is not null ? "Gemini/Groq (pre-extracted)" : "Gemini/Groq + GroqVision");
        observation.SetIdempotencyKey(request.IdempotencyKey);

        await _observationRepository.AddAsync(observation, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return observation.Id;
    }
}

public class RecordObservationCommandValidator : AbstractValidator<RecordObservationCommand>
{
    public RecordObservationCommandValidator()
    {
        RuleFor(x => x.FlockId).NotEqual(Guid.Empty);
        RuleFor(x => x.RawText).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.IdempotencyKey).NotEmpty();
    }
}
