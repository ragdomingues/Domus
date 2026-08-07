using Domus.Domain.Common;
using Domus.Domain.Enums;

namespace Domus.Domain.Entities;

public class ResidenceMembership : Entity
{
    public Guid UserId { get; private set; }
    public Guid ResidenceId { get; private set; }
    public ResidenceRole Role { get; private set; }
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset? ValidUntil { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public User? User { get; private set; }
    public Residence? Residence { get; private set; }

    private ResidenceMembership()
    {
    }

    public static ResidenceMembership Create(
        Guid userId,
        Guid residenceId,
        ResidenceRole role,
        DateTimeOffset? validFrom = null,
        DateTimeOffset? validUntil = null)
    {
        return new ResidenceMembership
        {
            UserId = userId,
            ResidenceId = residenceId,
            Role = role,
            ValidFrom = validFrom ?? DateTimeOffset.UtcNow,
            ValidUntil = validUntil
        };
    }

    public bool IsActiveAt(DateTimeOffset utcNow)
    {
        if (RevokedAt is not null)
        {
            return false;
        }

        if (utcNow < ValidFrom)
        {
            return false;
        }

        if (ValidUntil is not null && utcNow > ValidUntil)
        {
            return false;
        }

        return true;
    }

    public void Revoke(DateTimeOffset? at = null) => RevokedAt = at ?? DateTimeOffset.UtcNow;

    public void UpdateRole(ResidenceRole role, DateTimeOffset? validUntil = null)
    {
        Role = role;
        ValidUntil = validUntil;
    }

    public void Reactivate(ResidenceRole role, DateTimeOffset? validUntil = null)
    {
        Role = role;
        ValidUntil = validUntil;
        RevokedAt = null;
        ValidFrom = DateTimeOffset.UtcNow;
    }
}
