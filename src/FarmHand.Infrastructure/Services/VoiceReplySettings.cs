using FarmHand.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace FarmHand.Infrastructure.Services;

/// <summary>Reads the "Voice" feature flags from configuration.</summary>
public class VoiceReplySettings : IVoiceReplySettings
{
    public bool RespondWithVoice { get; }

    public VoiceReplySettings(IConfiguration configuration)
    {
        RespondWithVoice = configuration.GetValue("Voice:RespondWithVoice", true);
    }
}
