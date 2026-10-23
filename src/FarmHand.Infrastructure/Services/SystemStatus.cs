namespace FarmHand.Infrastructure.Services;

public class SystemStatus : ISystemStatus
{
    private readonly IFarmAiService _ai;
    private readonly IVisionAnalysisService _vision;
    private readonly ITranscriptionService _transcription;
    private readonly IPaymentService _payments;
    private readonly IEmailService _email;
    private readonly IImageStorage _storage;
    private readonly IConfiguration _configuration;

    public SystemStatus(
        IFarmAiService ai,
        IVisionAnalysisService vision,
        ITranscriptionService transcription,
        IPaymentService payments,
        IEmailService email,
        IImageStorage storage,
        IConfiguration configuration)
    {
        _ai = ai;
        _vision = vision;
        _transcription = transcription;
        _payments = payments;
        _email = email;
        _storage = storage;
        _configuration = configuration;
    }

    public IntegrationStatus GetIntegrationStatus()
    {
        var demoFallback = _configuration.GetValue("Ai:DemoFallback", false);
        var aiMode = _ai.IsAiConfigured ? "Live" : demoFallback ? "RuleBasedDemo" : "Unavailable";
        return new IntegrationStatus(
            aiMode,
            _ai.IsAiConfigured,
            _vision.IsConfigured,
            _transcription.IsConfigured,
            demoFallback,
            _payments.Mode,
            _email.IsEnabled,
            _storage.Provider,
            _configuration.GetValue("Demo:Enabled", false));
    }
}
