using FarmHand.Application.Common.Exceptions;
using FarmHand.Application.Common.Services;
using Microsoft.Extensions.Logging;

namespace FarmHand.Application.Features.Observations;

/// <summary>
/// Submits a health report from text, a voice note, a photo, or manual fields. The server transcribes,
/// analyses and structures the report, then stores it as a <b>pending</b> observation for the farmer to review.
/// Re-sending the same <paramref name="IdempotencyKey"/> returns the existing observation instead of a duplicate.
/// </summary>
/// <param name="FarmId">Owning farm; must belong to the caller.</param>
/// <param name="LivestockAnimalId">Individual animal, for animal-level reports.</param>
/// <param name="LivestockCategoryId">Flock or group, for flock-level reports.</param>
/// <param name="Text">The farmer's typed description.</param>
/// <param name="Audio">Voice note bytes (WebM, Ogg, MP4/M4A, MP3 or WAV; max 10 MB).</param>
/// <param name="AudioFileName">Original audio file name.</param>
/// <param name="Image">Photo bytes (JPEG, PNG, WebP or HEIC; max 8 MB).</param>
/// <param name="IdempotencyKey">Client-generated key that makes retries safe.</param>
/// <param name="ManualEntry">When true, AI is skipped and the supplied fields are stored as entered.</param>
/// <param name="MortalityCount">Manual mortality count.</param>
/// <param name="WaterConsumption">Manual water consumption.</param>
/// <param name="FeedConsumption">Manual feed consumption.</param>
/// <param name="EggProduction">Manual egg production.</param>
public record SubmitObservationCommand(
    Guid FarmId,
    Guid? LivestockAnimalId,
    Guid? LivestockCategoryId,
    string? Text,
    byte[]? Audio,
    string? AudioFileName,
    byte[]? Image,
    string IdempotencyKey,
    bool ManualEntry = false,
    int? MortalityCount = null,
    string? WaterConsumption = null,
    string? FeedConsumption = null,
    string? EggProduction = null) : IRequest<ObservationDetailDto>;

public class SubmitObservationCommandValidator : AbstractValidator<SubmitObservationCommand>
{
    public SubmitObservationCommandValidator()
    {
        RuleFor(x => x.FarmId).NotEqual(Guid.Empty);
        RuleFor(x => x)
            .Must(x => x.LivestockAnimalId is not null || x.LivestockCategoryId is not null)
            .WithName("Subject")
            .WithMessage("Choose the animal or the flock this report is about.");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Text).MaximumLength(2000);
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Text) || x.Audio is { Length: > 0 } || x.Image is { Length: > 0 })
            .WithName("Report")
            .WithMessage("Describe the problem, record a voice note, or add a photo.");
        RuleFor(x => x.MortalityCount).InclusiveBetween(0, 10000).When(x => x.MortalityCount is not null);
        RuleFor(x => x.WaterConsumption).MaximumLength(100);
        RuleFor(x => x.FeedConsumption).MaximumLength(100);
        RuleFor(x => x.EggProduction).MaximumLength(100);
    }
}

public class SubmitObservationCommandHandler : IRequestHandler<SubmitObservationCommand, ObservationDetailDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly IFarmAiService _ai;
    private readonly IVisionAnalysisService _vision;
    private readonly ITranscriptionService _transcription;
    private readonly IImageStorage _imageStorage;
    private readonly IAuditLogger _audit;
    private readonly ObservationProjector _projector;
    private readonly ILogger<SubmitObservationCommandHandler> _logger;

    public SubmitObservationCommandHandler(
        IUnitOfWork unitOfWork,
        IResourceAuthorizationService authorization,
        IFarmAiService ai,
        IVisionAnalysisService vision,
        ITranscriptionService transcription,
        IImageStorage imageStorage,
        IAuditLogger audit,
        ObservationProjector projector,
        ILogger<SubmitObservationCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _ai = ai;
        _vision = vision;
        _transcription = transcription;
        _imageStorage = imageStorage;
        _audit = audit;
        _projector = projector;
        _logger = logger;
    }

    public async Task<ObservationDetailDto> Handle(SubmitObservationCommand request, CancellationToken ct)
    {
        await _authorization.EnsureFarmAccessAsync(request.FarmId, ct);
        var subject = await ResolveSubjectAsync(request, ct);

        var key = request.IdempotencyKey.Trim();
        var existing = await _unitOfWork.PoultryObservations.GetByIdempotencyKeyAsync(key, ct);
        if (existing is not null)
        {
            if (existing.FarmId != request.FarmId)
                throw PublicException.Conflict("This idempotency key was already used for another report. Generate a new key.");
            return await _projector.DetailAsync(existing, reused: true, ct);
        }

        var imageType = request.Image is { Length: > 0 } image ? UploadRules.ValidateImage(image) : null;
        var audioType = request.Audio is { Length: > 0 } audio ? UploadRules.ValidateAudio(audio) : null;
        var channels = new List<string>();

        // 1. Voice note -> transcript (skipped for manual entry, where the farmer types instead).
        string? transcript = null;
        string? sttLanguage = null;
        if (audioType is not null && !request.ManualEntry)
        {
            var result = await _transcription.TranscribeAsync(request.Audio!, audioType, request.AudioFileName ?? "voice-note", key, ct);
            transcript = result.Transcript;
            sttLanguage = result.DetectedLanguage;
            channels.Add("voice");
        }

        var typed = request.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(typed)) channels.Add("text");
        var reportText = string.Join("\n", new[] { typed, transcript }.Where(s => !string.IsNullOrWhiteSpace(s)));

        // 2. Photo -> description (never fails the report; an unavailable result is stored and labelled).
        VisionAnalysisResult? vision = null;
        if (imageType is not null)
        {
            channels.Add("photo");
            if (!request.ManualEntry)
                vision = await _vision.AnalyzeImageAsync(request.Image!, imageType, reportText, ct);
        }

        // 3. Structure the report.
        ObservationExtraction extraction;
        if (request.ManualEntry)
        {
            extraction = new ObservationExtraction(
                new ExtractedObservation(
                    request.MortalityCount ?? RuleBasedAnalyzer.ExtractMortality(reportText) ?? 0,
                    Blank(request.WaterConsumption),
                    Blank(request.FeedConsumption),
                    Blank(request.EggProduction),
                    Blank(reportText)),
                RuleBasedAnalyzer.DetectSymptoms(reportText).Select(s => s.Label).ToList(),
                [],
                "manual");
        }
        else
        {
            var analysisText = reportText;
            if (string.IsNullOrWhiteSpace(analysisText))
            {
                if (vision is not { Available: true })
                    throw new AiUnavailableException("The photo could not be analysed right now. Describe what you see, or enter the details manually.");
                analysisText = $"Photo shows: {vision.Description}. Visible concerns: {(vision.DetectedIssues.Length == 0 ? "none noted" : string.Join(", ", vision.DetectedIssues))}.";
            }

            extraction = await _ai.ExtractObservationAsync(analysisText, subject.Description, ct);
        }

        // 4. Persist the photo only after analysis succeeded, so failed attempts leave nothing behind.
        StoredImage? stored = null;
        if (imageType is not null)
            stored = await _imageStorage.StoreAsync(request.Image!, imageType, "farmhand/observations", ct);

        var fields = extraction.Fields;
        var observation = new PoultryObservation(
            request.FarmId,
            request.LivestockAnimalId,
            subject.CategoryId,
            DateTime.UtcNow,
            fields.MortalityCount,
            fields.WaterConsumption,
            fields.FeedConsumption,
            fields.EggProduction,
            fields.FarmerNotes,
            stored?.Url,
            vision is null ? null : Mappers.SerializeVision(vision));

        // Remember the report's language (speech-to-text detection first, then AI detection of typed reports)
        // so the reply can be written back in the same language the farmer used.
        var reportLanguage = LanguageCodes.Normalize(sttLanguage) ?? LanguageCodes.Normalize(extraction.DetectedLanguage);
        if (reportLanguage is not null)
            observation.SetDetectedLanguage(reportLanguage);

        observation.MarkAiExtracted(extraction.Source);
        observation.SetExtractionDetails(Mappers.SerializeExtraction(extraction));
        observation.SetInputChannels(channels.Count == 0 ? "text" : string.Join(",", channels));
        if (!string.IsNullOrWhiteSpace(reportText))
            observation.SetRawTranscript(reportText.Length > 2000 ? reportText[..2000] : reportText);
        observation.SetIdempotencyKey(key);

        await _unitOfWork.PoultryObservations.AddAsync(observation, ct);
        await _audit.RecordAsync("observation.submitted", observation.Id, $"channels={observation.InputChannels}; source={extraction.Source}", ct: ct);

        try
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            // A concurrent retry with the same key won the race; return its observation.
            _logger.LogInformation("Concurrent duplicate observation submission for key {Key}", key);
            var winner = await _unitOfWork.PoultryObservations.GetByIdempotencyKeyAsync(key, ct)
                ?? throw PublicException.Conflict("This report is already being saved. Refresh in a moment.");
            return await _projector.DetailAsync(winner, reused: true, ct);
        }

        return await _projector.DetailAsync(observation, reused: false, ct);
    }

    private sealed record Subject(Guid? CategoryId, string Description);

    private async Task<Subject> ResolveSubjectAsync(SubmitObservationCommand request, CancellationToken ct)
    {
        if (request.LivestockAnimalId is { } animalId)
        {
            var animal = await _unitOfWork.LivestockAnimals.GetByIdAsync(animalId, ct)
                ?? throw PublicException.NotFound("That animal was not found.");
            if (animal.FarmId != request.FarmId)
                throw PublicException.BadRequest("The selected animal does not belong to this farm.");
            if (request.LivestockCategoryId is { } categoryId && animal.CategoryId != categoryId)
                throw PublicException.BadRequest("The selected group does not match the animal.");

            var description = $"one {animal.Species.ToLowerInvariant()} named {animal.Name}"
                + (animal.AgeMonths is { } age ? $", about {age} months old" : string.Empty)
                + (string.IsNullOrWhiteSpace(animal.Breed) ? string.Empty : $", breed {animal.Breed}");
            return new Subject(animal.CategoryId, description);
        }

        var category = await _unitOfWork.LivestockCategories.GetByIdAsync(request.LivestockCategoryId!.Value, ct)
            ?? throw PublicException.NotFound("That flock or group was not found.");
        if (category.FarmId != request.FarmId)
            throw PublicException.BadRequest("The selected group does not belong to this farm.");

        var flock = $"the group \"{category.Name}\"" + (string.IsNullOrWhiteSpace(category.Species) ? string.Empty : $" of {category.Species}")
            + (category.AnimalCount > 0 ? $" ({category.AnimalCount} animals)" : string.Empty);
        return new Subject(category.Id, flock);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static bool IsUniqueViolation(Exception ex) =>
        ex.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
        || ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true;
}

/// <summary>Lists observations across the caller's farms (or one farm), newest first.</summary>
public record GetObservationsQuery(
    Guid? FarmId,
    ObservationConfirmationStatus? Status,
    Guid? AnimalId,
    Guid? CategoryId,
    int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<ObservationSummaryDto>>;

public class GetObservationsQueryHandler : IRequestHandler<GetObservationsQuery, PagedResult<ObservationSummaryDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly ObservationProjector _projector;

    public GetObservationsQueryHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization, ObservationProjector projector)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _projector = projector;
    }

    public async Task<PagedResult<ObservationSummaryDto>> Handle(GetObservationsQuery request, CancellationToken ct)
    {
        IReadOnlyCollection<Guid> farmIds;
        if (request.FarmId is { } farmId)
        {
            await _authorization.EnsureFarmAccessAsync(farmId, ct);
            farmIds = [farmId];
        }
        else
        {
            farmIds = await _authorization.GetAccessibleFarmIdsAsync(ct)
                ?? throw PublicException.BadRequest("Administrators must specify a farmId.");
        }

        var paging = new Paging(request.Page, request.PageSize);
        var page = await _unitOfWork.PoultryObservations.SearchAsync(
            new ObservationSearch(farmIds, request.Status, request.AnimalId, request.CategoryId, paging), ct);
        var items = await _projector.SummariesAsync(page.Items, ct);
        return new PagedResult<ObservationSummaryDto>(items, paging.Page, paging.PageSize, page.TotalCount,
            (int)Math.Ceiling(page.TotalCount / (double)paging.PageSize));
    }
}

public record GetObservationQuery(Guid ObservationId) : IRequest<ObservationDetailDto>;

public class GetObservationQueryHandler : IRequestHandler<GetObservationQuery, ObservationDetailDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly ObservationProjector _projector;

    public GetObservationQueryHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization, ObservationProjector projector)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _projector = projector;
    }

    public async Task<ObservationDetailDto> Handle(GetObservationQuery request, CancellationToken ct)
    {
        var observation = await _unitOfWork.PoultryObservations.GetByIdAsync(request.ObservationId, ct)
            ?? throw PublicException.NotFound("Observation not found.");
        await _authorization.EnsureFarmAccessAsync(observation.FarmId, ct);
        return await _projector.DetailAsync(observation, reused: false, ct);
    }
}

/// <summary>Discards a pending (unconfirmed) observation. Confirmed observations are part of the record and cannot be discarded.</summary>
public record DiscardObservationCommand(Guid ObservationId) : IRequest;

public class DiscardObservationCommandHandler : IRequestHandler<DiscardObservationCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IResourceAuthorizationService _authorization;
    private readonly IAuditLogger _audit;

    public DiscardObservationCommandHandler(IUnitOfWork unitOfWork, IResourceAuthorizationService authorization, IAuditLogger audit)
    {
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _audit = audit;
    }

    public async Task<Unit> Handle(DiscardObservationCommand request, CancellationToken ct)
    {
        var observation = await _unitOfWork.PoultryObservations.GetByIdAsync(request.ObservationId, ct)
            ?? throw PublicException.NotFound("Observation not found.");
        await _authorization.EnsureFarmAccessAsync(observation.FarmId, ct);

        if (!observation.IsPending)
            throw PublicException.Conflict("Only a report that has not been confirmed can be discarded.");

        observation.Discard();
        await _audit.RecordAsync("observation.discarded", observation.Id, ct: ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
