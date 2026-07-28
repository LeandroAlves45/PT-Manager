using Domain.Entities.Supplements;
using Domain.Exceptions;
using Xunit;

namespace Domain.UnitTests.Entities.Supplements;

public sealed class SupplementTests
{
    [Fact]
    public void Update_DeletedSupplement_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var supplement = new Supplement(null, "Creatine", null, "grams", now);
        supplement.SoftDelete(now);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            supplement.Update("Creatine monohydrate", null, "grams", now.AddMinutes(1)));
    }
}
