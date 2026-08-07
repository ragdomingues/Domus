using Domus.Application.Devices;
using Domus.Application.Residences;
using Domus.Application.Security;
using Domus.Domain.Enums;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Domus.Application.Tests;

public sealed class ResidenceSoftDeleteTests
{
    [Fact]
    public async Task Soft_delete_residence_cascades_devices_memberships_and_provisionings()
    {
        await using var db = TestFixture.CreateDb();
        var seed = await TestFixture.SeedAsync(db);
        var sp = TestFixture.CreateServices(db);
        var residences = new ResidenceService(
            db,
            sp.GetRequiredService<IAccessControlService>(),
            sp.GetRequiredService<IValidator<CreateResidenceRequest>>(),
            sp.GetRequiredService<IValidator<UpdateResidenceRequest>>());
        var devices = TestFixture.CreateDeviceService(db, sp);
        var provisioning = TestFixture.CreateProvisioningService(db, sp: sp);

        var device = await devices.CreateAsync(
            new CreateDeviceRequest(seed.Residence.Id, DeviceType.Gate, "Portão"),
            new DeviceActorContext(seed.User.Id, null, null));
        device.Succeeded.Should().BeTrue();

        var issued = await provisioning.IssueAsync(
            new IssueProvisioningRequest(device.Value!.Id),
            new DeviceActorContext(seed.User.Id, null, null));
        issued.Succeeded.Should().BeTrue();

        var result = await residences.SoftDeleteAsync(
            seed.Residence.Id,
            new ResidenceMembershipContext(seed.User.Id, null, null));

        result.Succeeded.Should().BeTrue();

        var deletedDevice = await db.Devices.IgnoreQueryFilters()
            .SingleAsync(d => d.Id == device.Value.Id);
        deletedDevice.IsDeleted.Should().BeTrue();
        deletedDevice.LifecycleStatus.Should().Be(DeviceLifecycleStatus.Deleted);

        var membership = await db.ResidenceMemberships
            .SingleAsync(m => m.ResidenceId == seed.Residence.Id);
        membership.RevokedAt.Should().NotBeNull();

        var pending = await db.DeviceProvisionings
            .SingleAsync(p => p.Id == issued.Value!.ProvisioningId);
        pending.Status.Should().Be(ProvisioningStatus.Revoked);
    }
}
