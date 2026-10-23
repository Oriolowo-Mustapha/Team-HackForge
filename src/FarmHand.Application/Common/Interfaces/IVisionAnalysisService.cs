namespace FarmHand.Application.Common.Interfaces;

/// <summary>
/// Image description returned by a vision model. <see cref="Available"/> is false when no vision
/// provider could analyse the image; <see cref="Confidence"/> is the model's self-reported value and
/// is not a calibrated probability.
/// </summary>
public record VisionAnalysisResult(
    string Description,
    string[] DetectedIssues,
    decimal? Confidence,
    string Source,
    bool Available = true)
{
    public static VisionAnalysisResult Unavailable(string reason) =>
        new(reason, Array.Empty<string>(), null, "unavailable", false);
}

public interface IVisionAnalysisService
{
    bool IsConfigured { get; }

    Task<VisionAnalysisResult> AnalyzeImageAsync(byte[] image, string contentType, string farmerNotes, CancellationToken ct = default);
}
