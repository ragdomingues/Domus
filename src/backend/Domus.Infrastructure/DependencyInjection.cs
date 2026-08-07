using Domus.Application.Abstractions;
using Domus.Application.Auth;
using Domus.Application.Devices;
using Domus.Infrastructure.Messaging;
using Domus.Infrastructure.Notifications;
using Domus.Infrastructure.Persistence;
using Domus.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Domus.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<MqttOptions>(configuration.GetSection(MqttOptions.SectionName));
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.Configure<SmtpOptions>(configuration.GetSection(SmtpOptions.SectionName));
        services.Configure<ExpoPushOptions>(configuration.GetSection(ExpoPushOptions.SectionName));
        services.PostConfigure<ExpoPushOptions>(opts =>
        {
            opts.AccessToken = FirstNonEmpty(opts.AccessToken, configuration["EXPO_ACCESS_TOKEN"]);
            opts.ApiUrl = FirstNonEmpty(opts.ApiUrl, configuration["EXPO_PUSH_API_URL"])
                ?? "https://exp.host/--/api/v2/push/send";
        });

        services.Configure<HistoryRetentionOptions>(configuration.GetSection(HistoryRetentionOptions.SectionName));
        services.PostConfigure<HistoryRetentionOptions>(opts =>
        {
            if (int.TryParse(configuration["HISTORY_RETENTION_DAYS"], out var days) && days > 0)
            {
                opts.RetentionDays = days;
            }

            if (int.TryParse(configuration["HISTORY_RETENTION_INTERVAL_HOURS"], out var hours) && hours > 0)
            {
                opts.IntervalHours = hours;
            }
        });

        // Mesmos nomes do TopInvest: SMTP_HOST, SMTP_PORT, SMTP_SECURE (0/1), SMTP_USER, SMTP_PASS, MAIL_FROM, APP_PUBLIC_URL
        services.PostConfigure<SmtpOptions>(opts =>
        {
            opts.Host = FirstNonEmpty(opts.Host, configuration["SMTP_HOST"]) ?? string.Empty;
            opts.MailFrom = FirstNonEmpty(opts.MailFrom, configuration["MAIL_FROM"]) ?? string.Empty;
            opts.User = FirstNonEmpty(opts.User, configuration["SMTP_USER"]) ?? string.Empty;
            // Compose pode deixar aspas literais na senha
            opts.Pass = StripQuotes(FirstNonEmpty(opts.Pass, configuration["SMTP_PASS"]) ?? string.Empty);
            opts.AppPublicUrl = FirstNonEmpty(opts.AppPublicUrl, configuration["APP_PUBLIC_URL"]) ?? string.Empty;

            if (int.TryParse(FirstNonEmpty(configuration["SMTP_PORT"], opts.Port.ToString()), out var port) && port > 0)
            {
                opts.Port = port;
            }

            // TopInvest usa 0/1 — não bindar em Smtp:Secure (bool) diretamente
            var secureRaw = (configuration["SMTP_SECURE"] ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(secureRaw))
            {
                opts.Secure = secureRaw is "1" or "true" or "TRUE" or "True";
            }
        });

        services.PostConfigure<AuthOptions>(opts =>
        {
            var expose = configuration["AUTH_EXPOSE_RESET_TOKEN"];
            if (expose is null)
            {
                return;
            }

            opts.ExposeResetToken = expose is "1" or "true" or "TRUE" or "True";
        });

        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' não configurada.");

        services.AddDbContext<DomusDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IDomusDbContext>(sp => sp.GetRequiredService<DomusDbContext>());
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<ISecretHasher, Sha256SecretHasher>();
        services.AddSingleton<ISecureTokenGenerator, SecureTokenGenerator>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<LoggingEmailSender>();
        services.AddSingleton<SmtpEmailSender>();
        services.AddSingleton<IEmailSender, CompositeEmailSender>();
        services.AddHttpClient<IPushNotificationSender, ExpoPushNotificationSender>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate");
        });

        var mqtt = configuration.GetSection(MqttOptions.SectionName).Get<MqttOptions>() ?? new MqttOptions();
        services.AddSingleton(new MqttServiceCredentials
        {
            Username = mqtt.Username,
            Password = mqtt.Password
        });

        services.AddSingleton<MqttConnectionService>();
        services.AddSingleton<IMqttConnectionService>(sp => sp.GetRequiredService<MqttConnectionService>());
        services.AddHostedService(sp => sp.GetRequiredService<MqttConnectionService>());
        services.AddHostedService<CommandProcessingWorker>();
        services.AddHostedService<DevicePresenceWorker>();
        services.AddHostedService<GateOpenAlertWorker>();
        services.AddHostedService<HistoryRetentionWorker>();

        if (mqtt.Enabled)
        {
            services.AddSingleton<IDeviceMessenger, MqttDeviceMessenger>();
        }
        else
        {
            services.AddSingleton<IDeviceMessenger, NullDeviceMessenger>();
        }

        return services;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string StripQuotes(string value)
    {
        if (value.Length >= 2 &&
            ((value.StartsWith('"') && value.EndsWith('"')) ||
             (value.StartsWith('\'') && value.EndsWith('\''))))
        {
            return value[1..^1];
        }

        return value;
    }
}
