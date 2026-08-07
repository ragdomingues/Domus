using Domus.Application.Residences;
using Domus.Domain.Enums;
using Domus.Infrastructure.Security;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Domus.Application.Tests;

public class MembershipServiceTests
{
    [Fact]
    public async Task Admin_can_invite_new_user_and_returns_temporary_password()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db);
        var members = CreateService(db);

        var invited = await members.InviteAsync(
            new InviteMemberRequest(seed.Residence.Id, "membro@domus.test", "Membro", ResidenceRole.Member),
            new ResidenceMembershipContext(seed.User.Id, null, null));

        invited.Succeeded.Should().BeTrue();
        invited.Value!.CreatedNewUser.Should().BeTrue();
        invited.Value.TemporaryPassword.Should().NotBeNullOrWhiteSpace();

        (await db.Users.CountAsync(u => u.Email == "membro@domus.test")).Should().Be(1);
        (await db.ResidenceMemberships.CountAsync(m => m.ResidenceId == seed.Residence.Id)).Should().Be(2);
    }

    [Fact]
    public async Task Member_cannot_invite()
    {
        await using var db = TestFixture.CreateDb();
        var admin = await TestFixture.SeedAsync(db, ResidenceRole.Administrator);
        var member = await TestFixture.SeedAsync(db, ResidenceRole.Member);
        db.ResidenceMemberships.Add(Domain.Entities.ResidenceMembership.Create(
            member.User.Id,
            admin.Residence.Id,
            ResidenceRole.Member));
        db.TenantMemberships.Add(Domain.Entities.TenantMembership.Create(
            member.User.Id,
            admin.Tenant.Id,
            TenantRole.Member));
        await db.SaveChangesAsync();

        var members = CreateService(db);
        var invited = await members.InviteAsync(
            new InviteMemberRequest(admin.Residence.Id, "x@test.com", null, ResidenceRole.Member),
            new ResidenceMembershipContext(member.User.Id, null, null));

        invited.Succeeded.Should().BeFalse();
        invited.ErrorCode.Should().Be("forbidden");
    }

    [Fact]
    public async Task Admin_can_list_and_revoke_member()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db);
        var members = CreateService(db);

        var invited = await members.InviteAsync(
            new InviteMemberRequest(seed.Residence.Id, "visitante@domus.test", "Visitante", ResidenceRole.Visitor, 3),
            new ResidenceMembershipContext(seed.User.Id, null, null));

        invited.Succeeded.Should().BeTrue();

        var list = await members.ListAsync(seed.Residence.Id, seed.User.Id);
        list.Succeeded.Should().BeTrue();
        list.Value!.Count.Should().Be(2);

        var revoked = await members.RevokeAsync(
            seed.Residence.Id,
            invited.Value!.MembershipId,
            new ResidenceMembershipContext(seed.User.Id, null, null));

        revoked.Succeeded.Should().BeTrue();
        var membership = await db.ResidenceMemberships.FirstAsync(m => m.Id == invited.Value.MembershipId);
        membership.RevokedAt.Should().NotBeNull();
    }

    private static MembershipService CreateService(Domus.Infrastructure.Persistence.DomusDbContext db)
    {
        var sp = TestFixture.CreateServices(db);
        return new MembershipService(
            db,
            sp.GetRequiredService<Domus.Application.Security.IAccessControlService>(),
            new Argon2PasswordHasher(),
            sp.GetRequiredService<Domus.Application.Abstractions.ISecureTokenGenerator>(),
            sp.GetRequiredService<Domus.Application.Abstractions.IDateTimeProvider>(),
            sp.GetRequiredService<IValidator<InviteMemberRequest>>());
    }
}
