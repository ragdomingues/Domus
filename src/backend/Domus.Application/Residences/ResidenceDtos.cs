using Domus.Domain.Enums;

namespace Domus.Application.Residences;

public sealed record CreateResidenceRequest(Guid TenantId, string Name, string? Timezone, string? Address);
public sealed record UpdateResidenceRequest(string Name, string Timezone, string? Address);

public sealed record ResidenceResponse(
    Guid Id,
    Guid TenantId,
    string Name,
    string Timezone,
    string? Address,
    DateTimeOffset CreatedAt);

public sealed record ResidenceMembershipContext(Guid UserId, string? IpAddress, string? UserAgent);
