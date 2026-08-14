using Domain.Entities.Training;
using Domain.Exceptions;

namespace Domain.UnitTests.Entities.Training;

public sealed class TrainingPlanAggregateTests
{
    private static readonly DateTime Now =
        new(2026, 8, 12, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_EndDateOmitted_CreatesOpenEndedActivePlan()
    {
        var plan = CreatePlan(endDate: null);

        Assert.Null(plan.EndDate);
        Assert.True(plan.IsActive);
        Assert.False(plan.IsArchived);
    }

    [Fact]
    public void AddDay_DuplicateWeekAndWeekday_ThrowsDomainException()
    {
        var plan = CreatePlan();
        plan.AddDay(1, 1, null, Now);

        Assert.Throws<DomainException>(() => plan.AddDay(1, 1, null, Now));
    }

    [Fact]
    public void RemoveDay_ExistingDay_RemovesWholeOwnedBranch()
    {
        var plan = CreatePlan();
        var day = plan.AddDay(1, 1, null, Now);
        var exercise = day.AddExercise(Guid.NewGuid(), 1, null, null, null, Now);
        exercise.AddSet(1, 10, 20m, 60, 90, Now);

        plan.RemoveDay(day.Id, Now.AddMinutes(1));

        Assert.Empty(plan.Days);
    }

    [Fact]
    public void UpdateExercise_VisualOrderAndNotes_PreservesIdentityAndSets()
    {
        var plan = CreatePlan();
        var day = plan.AddDay(1, 1, null, Now);
        var prescribed = day.AddExercise(
            Guid.NewGuid(), 1, null, null, "Initial", Now);
        var set = prescribed.AddSet(1, 10, 20m, 60, 90, Now);

        day.UpdateExercise(
            prescribed.Id,
            prescribed.ExerciseId,
            2,
            prescribed.ExerciseGroupId,
            prescribed.GroupPosition,
            "Corrected",
            Now.AddMinutes(1));

        Assert.Equal(2, prescribed.OrderNumber);
        Assert.Equal("Corrected", prescribed.Notes);
        Assert.Equal(set.Id, Assert.Single(prescribed.Sets).Id);
    }

    [Fact]
    public void UpdateSet_DuplicateSetNumber_ThrowsDomainException()
    {
        var plan = CreatePlan();
        var exercise = plan.AddDay(1, 1, null, Now)
            .AddExercise(Guid.NewGuid(), 1, null, null, null, Now);
        var first = exercise.AddSet(1, 8, 40m, 60, 90, Now);
        exercise.AddSet(2, 8, 40m, 60, 90, Now);

        Assert.Throws<DomainException>(() => exercise.UpdateSet(
            first.Id, 2, 8, 40m, 60, 90, Now.AddMinutes(1)));
    }

    [Fact]
    public void Archive_RepeatedCall_IsIdempotent()
    {
        var plan = CreatePlan();
        var archivedAt = Now.AddMinutes(1);
        plan.Archive(archivedAt);

        plan.Archive(Now.AddMinutes(2));

        Assert.True(plan.IsArchived);
        Assert.False(plan.IsActive);
        Assert.Equal(archivedAt, plan.UpdatedAt);
    }

    [Fact]
    public void ArchivedPlan_StructureMutation_ThrowsDomainException()
    {
        var plan = CreatePlan();
        plan.Archive(Now);

        Assert.Throws<DomainException>(() =>
            plan.AddDay(1, 1, null, Now.AddMinutes(1)));
    }

    [Fact]
    public void ExerciseSetLog_Correct_PreservesIdentityReferenceSetAndCreatedAt()
    {
        var performedAt = new DateTimeOffset(Now);
        var log = new ClientExerciseSetLog(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            50m,
            10,
            null,
            performedAt,
            Now);
        var original = (log.Id, log.ClientId, log.TrainingPlanDayExerciseId,
            log.SetNumber, log.CreatedAt);

        log.Correct(
            55m,
            8,
            "Technique corrected",
            performedAt.AddMinutes(2),
            Now.AddMinutes(3));

        Assert.Equal(original, (log.Id, log.ClientId, log.TrainingPlanDayExerciseId,
            log.SetNumber, log.CreatedAt));
        Assert.Equal(55m, log.WeightKg);
        Assert.Equal(8, log.RepsDone);
        Assert.Equal(performedAt.AddMinutes(2), log.PerformedAt);
    }

    private static TrainingPlan CreatePlan(DateOnly? endDate = null) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Strength block",
        null,
        "Strength",
        null,
        new DateOnly(2026, 8, 1),
        endDate,
        Now);
}
