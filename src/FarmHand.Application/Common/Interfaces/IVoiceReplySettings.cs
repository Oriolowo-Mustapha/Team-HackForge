namespace FarmHand.Application.Common.Interfaces;

/// <summary>Feature flags for spoken replies (bound to the "Voice" configuration section).</summary>
public interface IVoiceReplySettings
{
    /// <summary>
    /// When true, confirming a report that arrived as a voice note also returns a spoken
    /// version of the assessment (best effort; the text reply is always present regardless).
    /// </summary>
    bool RespondWithVoice { get; }
}
