using Domain.Entities.Supplements;
using Domain.Exceptions;
using Xunit;

namespace Domain.UnitTests.Entities.Supplements;

public sealed class ClientSupplementAssignmentTests
{
    private static readonly DateTime Now = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_WithValidInstructions_CreatesActiveAssignment()
    {
        // Arrange
        var assignment = CreateAssignment();

        // Assert
        Assert.Equal("5 g", assignment.ServingSize);
        Assert.Equal("After training", assignment.Timing);
        Assert.True(assignment.IsActive);
        Assert.False(assignment.IsDeleted);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithoutServingSize_ThrowsDomainException(string? value)
    {
        // Act & Assert
        var action = () => new ClientSupplementAssignment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            value!,
            "After training",
            null,
            Now);

        // Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void SoftDelete_DeactivatesAssignment()
    {
        // Arrange
        var assignment = CreateAssignment();

        // Act
        assignment.SoftDelete(Now.AddMinutes(1));

        // Assert
        Assert.True(assignment.IsDeleted);
        Assert.False(assignment.IsActive);
    }

    [Fact]
    public void UpdateInstructions_AfterSoftDelete_ThrowsDomainException()
    {
        // Arrange
        var assignment = CreateAssignment();
        assignment.SoftDelete(Now.AddMinutes(1));

        // Act & Assert
        var action = () => assignment.UpdateInstructions(
            "10 g",
            "With breakfast",
            null,
            Now.AddMinutes(2));

        Assert.Throws<DomainException>(action);
    }

    private static ClientSupplementAssignment CreateAssignment() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "5 g",
            "After training",
            "Drink with water",
            Now);
}
