using Domus.Domain.Entities;
using FluentAssertions;

namespace Domus.Domain.Tests;

public class ResidenceTimezoneTests
{
    [Fact]
    public void Create_requires_timezone()
    {
        var act = () => Residence.Create(Guid.NewGuid(), "Casa", " ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_accepts_iana_timezone()
    {
        var residence = Residence.Create(Guid.NewGuid(), "Casa", "America/Sao_Paulo");
        residence.Timezone.Should().Be("America/Sao_Paulo");
    }
}
