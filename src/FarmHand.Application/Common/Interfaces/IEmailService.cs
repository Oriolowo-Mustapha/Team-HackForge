namespace FarmHand.Application.Common.Interfaces;

public record EmailMessage(string To, string Subject, string HtmlBody, string? TextBody = null);

public interface IEmailService
{
    /// <summary>True when outbound email is configured and enabled.</summary>
    bool IsEnabled { get; }

    /// <summary>Sends an email. Returns false when delivery is disabled or fails.</summary>
    Task<bool> SendAsync(EmailMessage message, CancellationToken ct = default);
}
