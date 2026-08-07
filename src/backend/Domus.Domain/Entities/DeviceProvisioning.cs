using Domus.Domain.Common;
using Domus.Domain.Enums;

namespace Domus.Domain.Entities;

public class DeviceProvisioning : Entity
{
    public Guid DeviceId { get; private set; }
    public Guid TenantId { get; private set; }
    public string ProvisioningCodeHash { get; private set; } = string.Empty;
    public ProvisioningStatus Status { get; private set; } = ProvisioningStatus.Pending;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ActivatedAt { get; private set; }
    public string? ActivatedFromIp { get; private set; }

    public Device? Device { get; private set; }

    private DeviceProvisioning()
    {
    }

    public static DeviceProvisioning Create(
        Guid deviceId,
        Guid tenantId,
        string provisioningCodeHash,
        DateTimeOffset expiresAt)
    {
        return new DeviceProvisioning
        {
            DeviceId = deviceId,
            TenantId = tenantId,
            ProvisioningCodeHash = provisioningCodeHash,
            Status = ProvisioningStatus.Pending,
            ExpiresAt = expiresAt
        };
    }

    public bool CanActivate(DateTimeOffset utcNow) =>
        Status == ProvisioningStatus.Pending && utcNow <= ExpiresAt;

    public void Activate(string? fromIp, DateTimeOffset? at = null)
    {
        if (!CanActivate(at ?? DateTimeOffset.UtcNow))
        {
            throw new InvalidOperationException("Provisioning não pode ser ativado.");
        }

        Status = ProvisioningStatus.Activated;
        ActivatedAt = at ?? DateTimeOffset.UtcNow;
        ActivatedFromIp = fromIp;
    }

    public void MarkExpired()
    {
        if (Status == ProvisioningStatus.Pending)
        {
            Status = ProvisioningStatus.Expired;
        }
    }

    public void Revoke()
    {
        if (Status is ProvisioningStatus.Pending or ProvisioningStatus.Activated)
        {
            Status = ProvisioningStatus.Revoked;
        }
    }
}
