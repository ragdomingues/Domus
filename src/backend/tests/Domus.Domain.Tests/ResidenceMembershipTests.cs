using Domus.Domain.Entities;
using Domus.Domain.Enums;
using FluentAssertions;

namespace Domus.Domain.Tests;

public class ResidenceMembershipTests
{
    [Fact]
    public void IsActiveAt_returns_false_when_visitor_expired()
    {
        var now = DateTimeOffset.Parse("2026-08-06T18:00:00Z");
        var membership = ResidenceMembership.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ResidenceRole.Visitor,
            validFrom: now.AddDays(-2),
            validUntil: now.AddHours(-1));

        membership.IsActiveAt(now).Should().BeFalse();
    }

    [Fact]
    public void IsActiveAt_returns_true_for_valid_administrator()
    {
        var now = DateTimeOffset.UtcNow;
        var membership = ResidenceMembership.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ResidenceRole.Administrator,
            validFrom: now.AddMinutes(-1));

        membership.IsActiveAt(now).Should().BeTrue();
    }

    [Fact]
    public void IsActiveAt_returns_false_when_revoked()
    {
        var now = DateTimeOffset.UtcNow;
        var membership = ResidenceMembership.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ResidenceRole.Member);
        membership.Revoke(now);

        membership.IsActiveAt(now).Should().BeFalse();
    }
}
