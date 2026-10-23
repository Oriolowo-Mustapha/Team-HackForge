namespace FarmHand.Application.Common.Exceptions;

/// <summary>
/// Raised when an AI or speech provider is not configured or did not respond. The API maps it to
/// 503 with code <c>ai_unavailable</c> so clients can keep the farmer's input and offer manual entry.
/// </summary>
public class AiUnavailableException : Exception
{
    public AiUnavailableException(string message, Exception? inner = null) : base(message, inner) { }
}
