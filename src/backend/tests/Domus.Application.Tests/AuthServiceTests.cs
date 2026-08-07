using Domus.Application;
using Domus.Application.Abstractions;
using Domus.Application.Auth;
using Domus.Infrastructure.Persistence;
using Domus.Infrastructure.Security;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Domus.Application.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task Register_login_refresh_logout_flow_works()
    {
        await using var db = CreateDb();
        var auth = CreateAuthService(db);

        var register = await auth.RegisterAsync(
            new RegisterRequest("rafael@domus.test", "SenhaForte1!", "Rafael", "Tenant Rafael", "Casa Principal", "America/Sao_Paulo"),
            new AuthContextInfo("127.0.0.1", "tests", "unit"));

        register.Succeeded.Should().BeTrue();
        register.Value!.AccessToken.Should().NotBeNullOrWhiteSpace();
        register.Value.RefreshToken.Should().NotBeNullOrWhiteSpace();
        register.Value.TenantId.Should().NotBeEmpty();
        register.Value.ResidenceId.Should().NotBeNull();

        var residence = await db.Residences.SingleAsync();
        residence.Timezone.Should().Be("America/Sao_Paulo");

        var memberships = await db.TenantMemberships.CountAsync();
        memberships.Should().Be(1);

        var login = await auth.LoginAsync(
            new LoginRequest("rafael@domus.test", "SenhaForte1!", "unit"),
            new AuthContextInfo("127.0.0.1", "tests", "unit"));

        login.Succeeded.Should().BeTrue();

        var refresh = await auth.RefreshAsync(
            new RefreshRequest(login.Value!.RefreshToken),
            new AuthContextInfo("127.0.0.1", "tests", "unit"));

        refresh.Succeeded.Should().BeTrue();
        refresh.Value!.RefreshToken.Should().NotBe(login.Value.RefreshToken);

        var reused = await auth.RefreshAsync(
            new RefreshRequest(login.Value.RefreshToken),
            new AuthContextInfo("127.0.0.1", "tests", "unit"));
        reused.Succeeded.Should().BeFalse();
        reused.ErrorCode.Should().Be("refresh_reuse");

        var logout = await auth.LogoutAsync(
            new LogoutRequest(refresh.Value.RefreshToken),
            new AuthContextInfo("127.0.0.1", "tests", null));
        logout.Succeeded.Should().BeTrue();

        var audits = await db.SecurityAuditLogs.CountAsync();
        audits.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Login_fails_with_wrong_password()
    {
        await using var db = CreateDb();
        var auth = CreateAuthService(db);

        await auth.RegisterAsync(
            new RegisterRequest("user@domus.test", "SenhaForte1!", "User", "Tenant"),
            new AuthContextInfo(null, null, null));

        var login = await auth.LoginAsync(
            new LoginRequest("user@domus.test", "errada"),
            new AuthContextInfo(null, null, null));

        login.Succeeded.Should().BeFalse();
        login.ErrorCode.Should().Be("invalid_credentials");
    }

    [Fact]
    public async Task Forgot_and_reset_password_allows_login_with_new_password()
    {
        await using var db = CreateDb();
        var auth = CreateAuthService(db, exposeResetToken: true);

        await auth.RegisterAsync(
            new RegisterRequest("reset@domus.test", "SenhaAntiga1!", "Reset", "Tenant"),
            new AuthContextInfo(null, null, null));

        var forgot = await auth.ForgotPasswordAsync(
            new ForgotPasswordRequest("reset@domus.test"),
            new AuthContextInfo("127.0.0.1", "tests", null));

        forgot.Succeeded.Should().BeTrue();
        forgot.Value!.ResetToken.Should().NotBeNullOrWhiteSpace();

        var reset = await auth.ResetPasswordAsync(
            new ResetPasswordRequest(forgot.Value.ResetToken!, "SenhaNova1!"),
            new AuthContextInfo("127.0.0.1", "tests", null));

        reset.Succeeded.Should().BeTrue();

        (await auth.LoginAsync(
            new LoginRequest("reset@domus.test", "SenhaAntiga1!"),
            new AuthContextInfo(null, null, null))).Succeeded.Should().BeFalse();

        (await auth.LoginAsync(
            new LoginRequest("reset@domus.test", "SenhaNova1!"),
            new AuthContextInfo(null, null, null))).Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Forgot_password_for_unknown_email_still_succeeds_without_token()
    {
        await using var db = CreateDb();
        var auth = CreateAuthService(db, exposeResetToken: true);

        var forgot = await auth.ForgotPasswordAsync(
            new ForgotPasswordRequest("nobody@domus.test"),
            new AuthContextInfo(null, null, null));

        forgot.Succeeded.Should().BeTrue();
        forgot.Value!.ResetToken.Should().BeNull();
    }

    private static DomusDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<DomusDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DomusDbContext(options);
    }

    private static IAuthService CreateAuthService(DomusDbContext db, bool exposeResetToken = false)
    {
        var services = new ServiceCollection();
        services.AddApplication();
        var provider = services.BuildServiceProvider();

        var clock = new SystemDateTimeProvider();
        var hasher = new Argon2PasswordHasher();
        var secretHasher = new Sha256SecretHasher();
        var tokens = new JwtTokenService(
            Options.Create(new JwtOptions
            {
                SigningKey = "DOMUS_TEST_SIGNING_KEY_AT_LEAST_32_CHARS_LONG"
            }),
            clock);

        return new AuthService(
            db,
            hasher,
            tokens,
            clock,
            new SecureTokenGenerator(),
            secretHasher,
            new LoggingEmailSender(NullLogger<LoggingEmailSender>.Instance),
            Options.Create(new AuthOptions
            {
                ExposeResetToken = exposeResetToken,
                PasswordResetTokenMinutes = 30
            }),
            provider.GetRequiredService<IValidator<RegisterRequest>>(),
            provider.GetRequiredService<IValidator<LoginRequest>>(),
            provider.GetRequiredService<IValidator<RefreshRequest>>(),
            provider.GetRequiredService<IValidator<ForgotPasswordRequest>>(),
            provider.GetRequiredService<IValidator<ResetPasswordRequest>>(),
            provider.GetRequiredService<IValidator<UpdateProfileRequest>>(),
            provider.GetRequiredService<IValidator<ChangePasswordRequest>>());
    }
}
