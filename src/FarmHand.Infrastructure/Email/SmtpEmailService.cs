using FarmHand.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;

namespace FarmHand.Infrastructure.Email;

/// <summary>
/// SMTP email delivery. Disabled by default; enable with the <c>Email</c> configuration section.
/// Credentials are never logged or returned to callers.
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly ILogger<SmtpEmailService> _logger;
    private readonly bool _enabled;
    private readonly string _host;
    private readonly int _port;
    private readonly bool _useSsl;
    private readonly string _userName;
    private readonly string _password;
    private readonly string _fromAddress;
    private readonly string _fromName;

    public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
    {
        _logger = logger;
        _enabled = configuration.GetValue("Email:Enabled", false);
        _host = configuration["Email:Host"] ?? string.Empty;
        _port = configuration.GetValue("Email:Port", 587);
        _useSsl = configuration.GetValue("Email:UseSsl", true);
        _userName = configuration["Email:UserName"] ?? string.Empty;
        _password = configuration["Email:Password"] ?? string.Empty;
        _fromAddress = configuration["Email:FromAddress"] ?? "no-reply@farmhand.ai";
        _fromName = configuration["Email:FromName"] ?? "FarmHand";
    }

    public bool IsEnabled => _enabled && !string.IsNullOrWhiteSpace(_host) && !string.IsNullOrWhiteSpace(_fromAddress);

    public async Task<bool> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (!IsEnabled)
        {
            _logger.LogInformation(
                "Email delivery is disabled; skipped sending {Subject} to {Recipient}",
                message.Subject,
                message.To);
            return false;
        }

        try
        {
            using var client = new SmtpClient(_host, _port)
            {
                EnableSsl = _useSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = string.IsNullOrWhiteSpace(_userName)
            };

            if (!string.IsNullOrWhiteSpace(_userName))
                client.Credentials = new NetworkCredential(_userName, _password);

            using var mail = new MailMessage
            {
                From = new MailAddress(_fromAddress, _fromName),
                Subject = message.Subject,
                Body = message.HtmlBody,
                IsBodyHtml = true
            };
            mail.To.Add(message.To);

            if (!string.IsNullOrWhiteSpace(message.TextBody))
                mail.AlternateViews.Add(
                    AlternateView.CreateAlternateViewFromString(message.TextBody, null, "text/plain"));

            await client.SendMailAsync(mail, ct);
            return true;
        }
        catch (Exception ex)
        {
            // Log only delivery metadata; never the message body (it can contain credentials).
            _logger.LogError(ex, "Failed to send {Subject} to {Recipient}", message.Subject, message.To);
            return false;
        }
    }
}
