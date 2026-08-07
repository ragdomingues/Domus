using Domus.Application.Abstractions;
using Domus.Application.Auth;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Domus.Infrastructure.Security;

/// <summary>
/// Envio SMTP no mesmo contrato operacional do TopInvest (nodemailer):
/// SMTP_HOST / PORT / SECURE / USER / PASS + MAIL_FROM + APP_PUBLIC_URL.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendPasswordResetAsync(
        string email,
        string recipientName,
        string resetToken,
        TimeSpan validFor,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException("SMTP não configurado (Smtp:Host / Smtp:MailFrom).");
        }

        var minutes = Math.Max(1, (int)Math.Ceiling(validFor.TotalMinutes));
        var name = string.IsNullOrWhiteSpace(recipientName) ? "morador" : recipientName.Trim();
        var appUrl = (_options.AppPublicUrl ?? string.Empty).TrimEnd('/');

        var text = BuildTextBody(name, resetToken, minutes, appUrl);
        var html = BuildHtmlBody(name, resetToken, minutes, appUrl);

        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(_options.MailFrom));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = "Domus — redefinição de senha";
        message.Body = new BodyBuilder { TextBody = text, HtmlBody = html }.ToMessageBody();

        using var client = new SmtpClient();
        var secure = _options.Secure
            ? SecureSocketOptions.SslOnConnect
            : _options.Port == 587
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.StartTlsWhenAvailable;

        await client.ConnectAsync(_options.Host, _options.Port, secure, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_options.User) && !string.IsNullOrWhiteSpace(_options.Pass))
        {
            await client.AuthenticateAsync(_options.User, _options.Pass, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);

        _logger.LogInformation("E-mail de reset enviado para {Email}", email);
    }

    private static string BuildTextBody(string name, string token, int minutes, string appUrl)
    {
        var deepLink = $"domus://reset-password?resetToken={Uri.EscapeDataString(token)}";
        var webLine = string.IsNullOrWhiteSpace(appUrl)
            ? string.Empty
            : $"\nLink web: {appUrl}/reset-password?token={Uri.EscapeDataString(token)}\n";

        return
            $"Olá, {name}.\n\n" +
            "Recebemos um pedido para redefinir a senha da sua conta Domus.\n\n" +
            $"Abra no app: {deepLink}\n\n" +
            $"Ou use este token no aplicativo (válido por {minutes} minutos):\n{token}\n" +
            webLine +
            "\nSe não foi você, ignore este e-mail.\n\n— Equipe Domus";
    }

    private static string BuildHtmlBody(string name, string token, int minutes, string appUrl)
    {
        var deepLink = $"domus://reset-password?resetToken={Uri.EscapeDataString(token)}";
        var deepHref = System.Net.WebUtility.HtmlEncode(deepLink);
        var webHtml = string.IsNullOrWhiteSpace(appUrl)
            ? string.Empty
            : $"<p><a href=\"{appUrl}/reset-password?token={Uri.EscapeDataString(token)}\">Abrir no navegador</a></p>";

        return
            $"<p>Olá, <strong>{System.Net.WebUtility.HtmlEncode(name)}</strong>.</p>" +
            "<p>Recebemos um pedido para redefinir a senha da sua conta Domus.</p>" +
            $"<p><a href=\"{deepHref}\">Abrir no aplicativo Domus</a></p>" +
            $"<p>Ou use este token no aplicativo (válido por {minutes} minutos):</p>" +
            $"<p style=\"font-size:18px;letter-spacing:1px\"><code>{System.Net.WebUtility.HtmlEncode(token)}</code></p>" +
            webHtml +
            "<p>Se não foi você, ignore este e-mail.</p>" +
            "<p>— Equipe Domus</p>";
    }
}
