using Domain.Entities.Training;
using Domain.Exceptions;
using Xunit;

namespace Domain.UnitTests.Entities.Training;

public sealed class TrainingPlanDayExerciseTests
{
    /// <summary>Campos estáticos para os testes.</summary>
    private static readonly DateTime Now = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ExerciseGroupId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public void Constructor_IsolatedExercise_KeepsGroupingFieldsNull()
    {
        // Arrange & Act
        var exercise = new TrainingPlanDayExercise(
            Guid.NewGuid(),
            Guid.NewGuid(),
            2,
            null,
            null,
            null,
            Now
        );

        // Assert
        Assert.Null(exercise.ExerciseGroupId);
        Assert.Null(exercise.GroupPosition);
    }

    [Fact]
    public void Constructor_GroupedExercise_PreservesGroupAndPosition()
    {
        // Arrange & Act
        var exercise = new TrainingPlanDayExercise(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            ExerciseGroupId,
            1,
            null,
            Now
        );

        // Assert
        Assert.Equal(ExerciseGroupId, exercise.ExerciseGroupId);
        Assert.Equal(1, exercise.GroupPosition);
    }

    [Fact]
    public void Constructor_PositionWithoutGroup_ThrowsDomainException()
    {
        // Arrange & Act & Assert
        Assert.Throws<DomainException>(() =>
            new TrainingPlanDayExercise(
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                null,
                1,
                null,
                Now
            )
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_GroupWithoutPositivePosition_ThrowsDomainException(int order)
    {
        // Arrange & Act & Assert
        Assert.Throws<DomainException>(() =>
            new TrainingPlanDayExercise(
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                ExerciseGroupId,
                order,
                null,
                Now
            )
        );
    }

    [Fact]
    public void Constructor_NullGroupPositionWithGroup_ThrowsDomainException()
    {
        // Arrange & Act & Assert
        Assert.Throws<DomainException>(() =>
            new TrainingPlanDayExercise(
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                ExerciseGroupId,
                null,
                null,
                Now
            )
        );
    }

    [Fact]
    public void Constructor_GroupWithInvalidId_ThrowsDomainException()
    {
        // Arrange & Act & Assert
        Assert.Throws<DomainException>(() =>
            new TrainingPlanDayExercise(
                Guid.NewGuid(),
                Guid.NewGuid(),
                1,
                Guid.Empty,
                1,
                null,
                Now
            )
        );
    }
}
