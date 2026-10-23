using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Poultry;

/// <summary>Records a poultry observation from free text, optionally with an image.</summary>
/// <param name="FarmId">Owning farm id; must belong to the caller.</param>
/// <param name="LivestockAnimalId">Animal id for animal-level observations (optional for flock-level).</param>
/// <param name="LivestockCategoryId">Category id for flock-level observations.</param>
/// <param name="RawText">Farmer's raw text; sent to Gemini with Groq fallback.</param>
/// <param name="IdempotencyKey">Client key; reusing a key returns the existing observation.</param>
/// <param name="ImageUrl">Optional image URL that triggers vision analysis.</param>
/// <param name="PreExtracted">Optional pre-extracted fields to avoid a second AI call.</param>
/// <param name="PreAnalyzedVisionJson">Optional precomputed vision-analysis JSON.</param>
public record RecordObservationCommand(
    Guid FarmId,
    Guid? LivestockAnimalId,
    Guid? LivestockCategoryId,
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
    private readonly IResourceAuthorizationService _authorization;

    public RecordObservationCommandHandler(
        IFarmAiService aiService,
        IVisionAnalysisService visionService,
        IPoultryObservationRepository observationRepository,
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization)
    {
        _aiService = aiService;
        _visionService = visionService;
        _observationRepository = observationRepository;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
    }

    public async Task<Guid> Handle(RecordObservationCommand request, CancellationToken ct)
    {
        await _authorization.EnsureFarmAccessAsync(request.FarmId, ct);

        if (request.LivestockAnimalId is { } animalId)
        {
            var animal = await _unitOfWork.LivestockAnimals.GetByIdAsync(animalId, ct)
                ?? throw new KeyNotFoundException($"Animal {animalId} not found.");
            if (animal.FarmId != request.FarmId)
                throw new ArgumentException("The selected animal does not belong to this farm.");

            if (request.LivestockCategoryId is { } animalCategoryId && animal.CategoryId != animalCategoryId)
                throw new ArgumentException("The selected category does not match the animal.");
        }
        else if (request.LivestockCategoryId is { } categoryId)
        {
            var category = await _unitOfWork.LivestockCategories.GetByIdAsync(categoryId, ct)
                ?? throw new KeyNotFoundException($"Category {categoryId} not found.");
            if (category.FarmId != request.FarmId)
                throw new ArgumentException("The selected category does not belong to this farm.");
        }
        else
        {
            throw new ArgumentException("Provide either a livestock animal or a category.");
        }

        // Idempotency: if this key was already used, return the existing observation
        var existing = await _observationRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey, ct);
        if (existing is not null)
        {
            await _authorization.EnsureFarmAccessAsync(existing.FarmId, ct);
            return existing.Id;
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
            request.FarmId,
            request.LivestockAnimalId,
            request.LivestockCategoryId,
            DateTime.UtcNow,
            extracted.MortalityCount,
            extracted.WaterConsumption,
            extracted.FeedConsumption,
            extracted.EggProduction,
            extracted.FarmerNotes,
            request.ImageUrl,
            visionAnalysis);

        observation.MarkAiExtracted(request.PreExtracted is not null ? "Gemini/Groq (pre-extracted)" : "Gemini/Groq + GroqVision");
        observation.SetRawTranscript(request.RawText);
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
        RuleFor(x => x.FarmId).NotEqual(Guid.Empty);
        RuleFor(x => x)
            .Must(x => x.LivestockAnimalId is not null || x.LivestockCategoryId is not null)
            .WithMessage("Provide either a livestock animal or a category.");
        RuleFor(x => x.LivestockAnimalId)
            .NotEqual(Guid.Empty)
            .When(x => x.LivestockAnimalId is not null);
        RuleFor(x => x.LivestockCategoryId)
            .NotEqual(Guid.Empty)
            .When(x => x.LivestockCategoryId is not null);
        RuleFor(x => x.RawText).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}
