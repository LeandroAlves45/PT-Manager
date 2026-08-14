using Domain.Entities.Training;
using Domain.Exceptions;
using Xunit;

namespace Domain.UnitTests.Entities.Training;

public class TrainingPlanTests
{
    [Fact]
    public void Constructor_WhenNameIsNull_ThrowsDomainException()
    {
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);

        var action = () => new TrainingPlan(
            Guid.NewGuid(), Guid.NewGuid(), null!, null, null, null,
            new DateOnly(2026, 8, 1), null, now);

        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void Constructor_StartAfterEnd_ThrowsDomainException()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var startsDate = new DateOnly(2026, 8, 10);
        var endsDate = new DateOnly(2026, 8, 1);

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => new TrainingPlan(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Training Plan",
            null,
            null,
            null,
            startsDate,
            endsDate,
            now
        ));
        Assert.Equal("Training plan end date cannot be before start date.", exception.Message);
    }

    [Fact]
    public void SoftDelete_ActivePlan_ClearsActiveAndSetsArchived()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var startsDate = new DateOnly(2026, 8, 1);
        var endsDate = new DateOnly(2026, 8, 10);

        var trainingPlan = new TrainingPlan(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Training Plan",
            null,
            null,
            null,
            startsDate,
            endsDate,
            now
        );

        // Act
        trainingPlan.SoftDelete(now.AddMinutes(1));

        // Assert
        Assert.True(trainingPlan.IsDeleted);
        Assert.False(trainingPlan.IsActive);
        Assert.True(trainingPlan.IsArchived);
    }

    [Fact]
    public void SoftDelete_RepeatedCall_IsIdempotent()
    {
        // Arrange
        var now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var trainingPlan = new TrainingPlan(
            Guid.NewGuid(), Guid.NewGuid(), "Test Training Plan", null, null,
            null, new DateOnly(2026, 8, 1), null, now);
        var deletedAt = now.AddMinutes(1);
        trainingPlan.SoftDelete(deletedAt);

        // Act
        trainingPlan.SoftDelete(now.AddMinutes(2));

        // Assert
        Assert.Equal(deletedAt, trainingPlan.UpdatedAt);
    }
}
