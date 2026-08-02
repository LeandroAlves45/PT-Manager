using Domain.Entities.Supplements;
using Domain.Exceptions;
using Xunit;

namespace Domain.UnitTests.Entities.Supplements;

public sealed class ClientSupplementAssignmentTests
{
    private static readonly DateTime Now = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_NormalizesPrescription()
    {
        // Arrange
        var assignment = new ClientSupplementAssignment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            " 5g ",
            " after workout ",
            null,
            Now
        );

        // Assert
        Assert.Equal(("5g", "after workout"), (assignment.Dose, assignment.TimingNotes));
    }

    [Fact]
    public void Constructor_WhenDoseExceedsLimit_ThrowsDomainException()
    {
        // Arrange
        var action = () => new ClientSupplementAssignment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new string('a', 101),
            null,
            null,
            Now
        );

        // Act & Assert
        Assert.Throws<DomainException>(action);
    }
}
