namespace Domus.Application.Auth;

/// <summary>
/// Configuração SMTP no mesmo padrão do TopInvest
/// (SMTP_HOST, SMTP_PORT, SMTP_SECURE, SMTP_USER, SMTP_PASS, MAIL_FROM, APP_PUBLIC_URL).
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool Secure { get; set; }
    public string User { get; set; } = string.Empty;
    public string Pass { get; set; } = string.Empty;
    public string MailFrom { get; set; } = string.Empty;

    /// <summary>URL pública da app (links opcionais no e-mail).</summary>
    public string AppPublicUrl { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(MailFrom);
}
