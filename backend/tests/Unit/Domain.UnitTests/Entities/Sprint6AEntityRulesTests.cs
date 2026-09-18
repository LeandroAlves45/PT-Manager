using Domain.Entities.Assessments;
using Domain.Entities.Nutrition;
using Domain.Entities.Supplements;
using Domain.Entities.Training;
using Domain.Exceptions;

namespace Domain.UnitTests.Entities;

/// <summary>Prova as invariantes de domínio acrescentadas na Sprint 6A.</summary>
public sealed class Sprint6AEntityRulesTests
{
    private static readonly DateTime Now = new(2026, 9, 16, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 9, 16);

    [Fact]
    public void ExerciseSet_WithValidPlannedRpe_StoresValue()
    {
        var set = new ExerciseSet(Guid.NewGuid(), 1, 8, 60m, 60, 90, Now, plannedRpe: 8.5m);

        Assert.Equal(8.5m, set.PlannedRpe);
    }

    [Fact]
    public void ExerciseSet_WithInvalidPlannedRpe_Throws() =>
        Assert.Throws<DomainException>(() =>
            new ExerciseSet(Guid.NewGuid(), 1, 8, 60m, 60, 90, Now, plannedRpe: 11m));

    [Fact]
    public void Prescription_UpdateSet_ReplacesPlannedRpe()
    {
        var prescription = new TrainingPlanDayExercise(Guid.NewGuid(), Guid.NewGuid(), 1, null, null, null, Now);
        var set = prescription.AddSet(1, 8, 60m, 60, 90, Now, plannedRpe: 7m);

        prescription.UpdateSet(set.Id, 1, 8, 60m, 60, 90, null, Now.AddMinutes(1));

        Assert.Null(set.PlannedRpe);
    }

    [Fact]
    public void ClientExerciseSetLog_WithInvalidRpe_Throws() =>
        Assert.Throws<DomainException>(() => new ClientExerciseSetLog(
            Guid.NewGuid(), Guid.NewGuid(), 1, 50m, 8, null, new DateTimeOffset(Now), Now, rpe: 0.5m));

    [Fact]
    public void ClientExerciseSetLog_Correct_ReplacesRpe()
    {
        var log = new ClientExerciseSetLog(
            Guid.NewGuid(), Guid.NewGuid(), 1, 50m, 8, null, new DateTimeOffset(Now), Now, rpe: 7m);

        log.Correct(52m, 8, 9.5m, null, log.PerformedAt, Now.AddMinutes(1));

        Assert.Equal(9.5m, log.Rpe);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(1000.01)]
    public void Food_WithDefaultServingOutsideRange_Throws(double grams) =>
        Assert.Throws<DomainException>(() =>
            new Food(Guid.NewGuid(), "Rice", null, 7m, 78m, 1m, null, Now, (decimal)grams));

    [Fact]
    public void Food_Update_ReplacesDefaultServing()
    {
        var food = new Food(Guid.NewGuid(), "Rice", null, 7m, 78m, 1m, null, Now, 150m);

        food.Update("Rice", null, 7m, 78m, 1m, null, null, Now.AddMinutes(1));

        Assert.Null(food.DefaultServingGrams);
    }

    [Fact]
    public void CheckIn_MarkReviewed_OnUnansweredCheckIn_Throws()
    {
        var checkIn = new CheckIn(Guid.NewGuid(), Guid.NewGuid(), Today, null, Now);

        Assert.Throws<DomainException>(() => checkIn.MarkReviewed(Now));
    }

    [Fact]
    public void CheckIn_MarkReviewed_IsIdempotentAndKeepsOriginalInstant()
    {
        var checkIn = new CheckIn(Guid.NewGuid(), Guid.NewGuid(), Today, null, Now);
        checkIn.SubmitResponse(70m, null, null, null, null, null, null, Today, Now);

        var first = checkIn.MarkReviewed(Now.AddHours(1));
        var second = checkIn.MarkReviewed(Now.AddHours(2));

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(Now.AddHours(1), checkIn.ReviewedAt);
    }

    [Fact]
    public void CheckIn_Correct_KeepsReviewedAt()
    {
        var checkIn = new CheckIn(Guid.NewGuid(), Guid.NewGuid(), Today, null, Now);
        checkIn.SubmitResponse(70m, null, null, null, null, null, null, Today, Now);
        checkIn.MarkReviewed(Now.AddHours(1));

        checkIn.Correct(null, 71m, null, null, null, null, null, null, Now.AddHours(2));

        Assert.Equal(Now.AddHours(1), checkIn.ReviewedAt);
    }

    [Fact]
    public void WorkoutCompletion_NormalizesNotesAndRejectsLongNotes()
    {
        var completion = new WorkoutCompletion(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Today, "  done  ", Now);

        Assert.Equal("done", completion.Notes);
        Assert.Equal(Now, completion.CompletedAt);
        Assert.Throws<DomainException>(() => new WorkoutCompletion(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Today, new string('x', 501), Now));
    }

    [Fact]
    public void WorkoutCompletion_WithEmptyIdentifiers_Throws() =>
        Assert.Throws<DomainException>(() => new WorkoutCompletion(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Today, null, Now));

    [Fact]
    public void ClientSupplementIntake_WithEmptyAssignment_Throws() =>
        Assert.Throws<DomainException>(() => new ClientSupplementIntake(
            Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Today, Now));
}
