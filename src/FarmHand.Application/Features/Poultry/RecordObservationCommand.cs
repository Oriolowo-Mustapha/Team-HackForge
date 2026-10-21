using FarmHand.Application.Common.Interfaces;
using FarmHand.Domain.Entities;
using FarmHand.Domain.Interfaces;
using MediatR;

namespace FarmHand.Application.Features.Poultry;

/// <summary>Records a poultry observation from free text, optionally with an image.</summary>
/// <param name="FlockId">Flock id.</param>
/// <param name="RawText">Farmer's raw text; sent to Gemini with Groq fallback.</param>
/// <param name="IdempotencyKey">Client key required by validation but not yet enforced.</param>
/// <param name="ImageUrl">Optional image URL that triggers vision analysis.</param>
public record RecordObservationCommand(
    Guid FlockId,
    string RawText,
    string IdempotencyKey,
    string? ImageUrl = null) : IRequest<Guid>;

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
        var extracted = await _aiService.ExtractObservationAsync(request.RawText, ct);

        string? visionAnalysis = null;
        if (!string.IsNullOrWhiteSpace(request.ImageUrl))
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

        observation.MarkAiExtracted("Gemini/Groq + GroqVision");

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
