namespace FarmHand.Application.Common.Interfaces;

public record VisionAnalysisResult(string Description, string[] DetectedIssues, decimal Confidence);

public interface IVisionAnalysisService
{
    Task<VisionAnalysisResult> AnalyzeImageAsync(string imageUrl, string farmerNotes, CancellationToken ct = default);
}
