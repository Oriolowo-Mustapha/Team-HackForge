using FarmHand.Application.Common.Interfaces;
using FarmHand.Infrastructure.AI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FarmHand.Infrastructure.Services;

public class VisionAnalysisService : IVisionAnalysisService
{
    private readonly GroqVisionService _groqVision;
    private readonly ILogger<VisionAnalysisService> _logger;

    public VisionAnalysisService(
        GroqVisionService groqVision,
        ILogger<VisionAnalysisService> logger)
    {
        _groqVision = groqVision;
        _logger = logger;
    }

    public async Task<VisionAnalysisResult> AnalyzeImageAsync(string imageUrl, string farmerNotes, CancellationToken ct = default)
    {
        try
        {
            var result = await _groqVision.AnalyzeImageAsync(imageUrl, farmerNotes, ct);
            return new VisionAnalysisResult(
                result.Description,
                result.DetectedIssues,
                result.Confidence);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vision analysis failed, continuing without image analysis");
            return new VisionAnalysisResult(
                "Vision analysis unavailable",
                Array.Empty<string>(),
                0);
        }
    }
}
