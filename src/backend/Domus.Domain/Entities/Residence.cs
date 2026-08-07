using Domus.Domain.Common;

namespace Domus.Domain.Entities;

public class Residence : SoftDeletableEntity
{
    public const string DefaultTimezone = "America/Sao_Paulo";

    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Timezone { get; private set; } = DefaultTimezone;
    public string? Address { get; private set; }

    public Tenant? Tenant { get; private set; }
    public ICollection<Device> Devices { get; private set; } = new List<Device>();
    public ICollection<ResidenceMembership> Memberships { get; private set; } = new List<ResidenceMembership>();

    private Residence()
    {
    }

    public static Residence Create(Guid tenantId, string name, string timezone, string? address = null)
    {
        if (string.IsNullOrWhiteSpace(timezone))
        {
            throw new ArgumentException("Timezone IANA é obrigatório.", nameof(timezone));
        }

        return new Residence
        {
            TenantId = tenantId,
            Name = name.Trim(),
            Timezone = timezone.Trim(),
            Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim()
        };
    }

    public void ChangeTimezone(string timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
        {
            throw new ArgumentException("Timezone IANA é obrigatório.", nameof(timezone));
        }

        Timezone = timezone.Trim();
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nome é obrigatório.", nameof(name));
        }

        Name = name.Trim();
    }

    public void SetAddress(string? address) =>
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
}
