using Domus.Domain.Common;

namespace Domus.Domain.Entities;

public class PasswordResetToken : Entity
{
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }

    public User? User { get; private set; }

    private PasswordResetToken()
    {
    }

    public static PasswordResetToken Create(Guid userId, string tokenHash, DateTimeOffset expiresAt)
    {
        return new PasswordResetToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt
        };
    }

    public bool IsUsable(DateTimeOffset utcNow) =>
        UsedAt is null && utcNow <= ExpiresAt;

    public void MarkUsed(DateTimeOffset? at = null) =>
        UsedAt = at ?? DateTimeOffset.UtcNow;
}
