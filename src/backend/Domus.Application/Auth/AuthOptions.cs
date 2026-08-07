namespace Domus.Application.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Em Development, devolve o token no JSON para o app concluir o fluxo sem SMTP.
    /// Manter false em produção.
    /// </summary>
    public bool ExposeResetToken { get; set; }

    public int PasswordResetTokenMinutes { get; set; } = 30;
}
