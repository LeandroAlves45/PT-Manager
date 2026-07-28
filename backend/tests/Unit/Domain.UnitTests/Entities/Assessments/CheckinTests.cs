using Domain.Entities.Assessments;
using Domain.Exceptions;
using Xunit;

namespace Domain.UnitTests.Entities.Assessments;

public sealed class CheckinTests
{
    [Fact]
    public void Correct_DeletedCheckin_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var checkin = new Checkin(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 7, 25),
            null,
            80,
            null,
            null,
            now
        );
        checkin.SoftDelete(now);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            checkin.Correct(null, 79, null, null, now.AddMinutes(1)));
    }
}
