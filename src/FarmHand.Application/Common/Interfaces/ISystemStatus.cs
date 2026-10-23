namespace FarmHand.Application.Common.Interfaces;

/// <summary>Non-secret description of which integrations are configured, for UI labelling and admin health.</summary>
public record IntegrationStatus(
    string AiMode,
    bool AiTextConfigured,
    bool AiVisionConfigured,
    bool TranscriptionConfigured,
    bool RuleBasedDemoFallbackEnabled,
    string PaymentsMode,
    bool EmailEnabled,
    string ImageStorage,
    bool DemoMode);

public interface ISystemStatus
{
    IntegrationStatus GetIntegrationStatus();
}
