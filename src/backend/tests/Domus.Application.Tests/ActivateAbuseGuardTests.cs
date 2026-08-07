using Domus.Application.Security;
using Domus.Infrastructure.Security;
using FluentAssertions;

namespace Domus.Application.Tests;

public class ActivateAbuseGuardTests
{
    [Fact]
    public void Blocks_after_repeated_failures_for_same_code()
    {
        var clock = new TestFixture.FakeClock(DateTimeOffset.UtcNow);
        var guard = new ActivateAbuseGuard(new Sha256SecretHasher(), clock);

        for (var i = 0; i < 8; i++)
        {
            guard.RegisterFailure("1.1.1.1", "code-A", "hw-1");
        }

        var result = guard.EnsureAllowed("1.1.1.1", "code-A", "hw-1");
        result.Succeeded.Should().BeFalse();
        result.ErrorCode.Should().Be("activate_rate_limited");
    }
}
