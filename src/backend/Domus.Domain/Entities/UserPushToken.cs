using Domus.Domain.Common;

namespace Domus.Domain.Entities;

public class UserPushToken : Entity
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public string Platform { get; private set; } = "unknown";
    public string? DeviceName { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAt { get; private set; }

    public User? User { get; private set; }

    private UserPushToken()
    {
    }

    public static UserPushToken Create(Guid userId, string token, string platform, string? deviceName = null)
    {
        return new UserPushToken
        {
            UserId = userId,
            Token = token.Trim(),
            Platform = NormalizePlatform(platform),
            DeviceName = string.IsNullOrWhiteSpace(deviceName) ? null : deviceName.Trim(),
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Touch(string platform, string? deviceName = null)
    {
        Platform = NormalizePlatform(platform);
        if (!string.IsNullOrWhiteSpace(deviceName))
        {
            DeviceName = deviceName.Trim();
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkUsed(DateTimeOffset? at = null) => LastUsedAt = at ?? DateTimeOffset.UtcNow;

    private static string NormalizePlatform(string platform) =>
        platform.Trim().ToLowerInvariant() switch
        {
            "ios" => "ios",
            "android" => "android",
            "web" => "web",
            _ => "unknown"
        };
}
