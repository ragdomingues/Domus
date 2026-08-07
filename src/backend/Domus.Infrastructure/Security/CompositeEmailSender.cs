using Domus.Application.Abstractions;
using Domus.Application.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Domus.Infrastructure.Security;

/// <summary>
/// Usa SMTP quando configurado; senão faz fallback para log (dev),
/// espelhando o comportamento opcional do TopInvest.
/// </summary>
public sealed class CompositeEmailSender : IEmailSender
{
    private readonly SmtpOptions _smtp;
    private readonly SmtpEmailSender _smtpSender;
    private readonly LoggingEmailSender _loggingSender;
    private readonly ILogger<CompositeEmailSender> _logger;

    public CompositeEmailSender(
        IOptions<SmtpOptions> smtpOptions,
        SmtpEmailSender smtpSender,
        LoggingEmailSender loggingSender,
        ILogger<CompositeEmailSender> logger)
    {
        _smtp = smtpOptions.Value;
        _smtpSender = smtpSender;
        _loggingSender = loggingSender;
        _logger = logger;
    }

    public async Task SendPasswordResetAsync(
        string email,
        string recipientName,
        string resetToken,
        TimeSpan validFor,
        CancellationToken cancellationToken = default)
    {
        if (!_smtp.IsConfigured)
        {
            _logger.LogWarning(
                "SMTP não configurado — reset de senha apenas em log (defina Smtp:Host e Smtp:MailFrom).");
            await _loggingSender.SendPasswordResetAsync(email, recipientName, resetToken, validFor, cancellationToken);
            return;
        }

        try
        {
            await _smtpSender.SendPasswordResetAsync(email, recipientName, resetToken, validFor, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha SMTP ao enviar reset para {Email} — registrando token em log.", email);
            await _loggingSender.SendPasswordResetAsync(email, recipientName, resetToken, validFor, cancellationToken);
        }
    }
}
