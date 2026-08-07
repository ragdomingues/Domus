using Domus.Domain.Common;
using Domus.Domain.Enums;

namespace Domus.Domain.Entities;

public class Tenant : SoftDeletableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public TenantStatus Status { get; private set; } = TenantStatus.Active;

    public ICollection<Residence> Residences { get; private set; } = new List<Residence>();
    public ICollection<TenantMembership> Memberships { get; private set; } = new List<TenantMembership>();

    private Tenant()
    {
    }

    public static Tenant Create(string name, string slug)
    {
        return new Tenant
        {
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            Status = TenantStatus.Active
        };
    }
}
