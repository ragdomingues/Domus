using Domus.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Domus.Infrastructure.Security;

/// <summary>
/// Placeholder de e-mail: registra o token de reset nos logs (útil em Development).
/// Substituir por provedor SMTP/API em produção.
/// </summary>
public sealed class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendPasswordResetAsync(
        string email,
        string recipientName,
        string resetToken,
        TimeSpan validFor,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Password reset para {Email} ({Name}). Token={Token}. Válido por {Minutes} min.",
            email,
            recipientName,
            resetToken,
            (int)validFor.TotalMinutes);

        return Task.CompletedTask;
    }
}
