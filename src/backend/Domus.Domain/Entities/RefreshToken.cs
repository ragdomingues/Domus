using Domus.Domain.Common;

namespace Domus.Domain.Entities;

public class RefreshToken : Entity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public Guid FamilyId { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public string? DeviceInfo { get; private set; }
    public string? IpAddress { get; private set; }

    public User? User { get; private set; }

    private RefreshToken()
    {
    }

    public static RefreshToken Create(
        Guid userId,
        string tokenHash,
        Guid familyId,
        DateTimeOffset expiresAt,
        string? deviceInfo = null,
        string? ipAddress = null)
    {
        return new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            FamilyId = familyId,
            ExpiresAt = expiresAt,
            DeviceInfo = deviceInfo,
            IpAddress = ipAddress
        };
    }

    public bool IsActive(DateTimeOffset utcNow) =>
        RevokedAt is null && utcNow <= ExpiresAt;

    public void Revoke(Guid? replacedByTokenId = null, DateTimeOffset? at = null)
    {
        RevokedAt = at ?? DateTimeOffset.UtcNow;
        ReplacedByTokenId = replacedByTokenId;
    }
}
