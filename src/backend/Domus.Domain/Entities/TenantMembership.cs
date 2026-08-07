using Domus.Domain.Common;
using Domus.Domain.Enums;

namespace Domus.Domain.Entities;

public class TenantMembership : Entity
{
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public TenantRole Role { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public User? User { get; private set; }
    public Tenant? Tenant { get; private set; }

    private TenantMembership()
    {
    }

    public static TenantMembership Create(Guid userId, Guid tenantId, TenantRole role)
    {
        return new TenantMembership
        {
            UserId = userId,
            TenantId = tenantId,
            Role = role
        };
    }

    public bool IsActive => RevokedAt is null;

    public void Revoke(DateTimeOffset? at = null) => RevokedAt = at ?? DateTimeOffset.UtcNow;

    public void Reactivate(TenantRole role)
    {
        Role = role;
        RevokedAt = null;
    }
}
