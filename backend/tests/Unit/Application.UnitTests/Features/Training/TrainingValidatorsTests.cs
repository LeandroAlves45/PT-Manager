using Application.Features.Training.ExerciseSetLogs.CorrectExerciseSetLog;
using Application.Features.Training.ExerciseSetLogs.ListExerciseSetLogs;
using Application.Features.Training.ExerciseSetLogs.RecordExerciseSetLog;
using Application.Features.Training.TrainingPlans;

namespace Application.UnitTests.Features.Training;

public sealed class TrainingValidatorsTests
{
    [Fact]
    public async Task NewStructure_ExistingIdentifier_IsRejected()
    {
        var validator = new TrainingPlanStructureValidator(
            requireNewIdentifiers: true);
        var input = Structure(dayId: Guid.NewGuid());

        var result = await validator.ValidateAsync(
            input,
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors,
            error => error.ErrorCode == "training_new_structure_id_forbidden");
    }

    [Fact]
    public async Task ReconciledStructure_DuplicateDaySlot_IsRejected()
    {
        var first = Day(Guid.NewGuid(), 1, 1);
        var second = Day(Guid.NewGuid(), 1, 1);
        var validator = new TrainingPlanStructureValidator(
            requireNewIdentifiers: false);

        var result = await validator.ValidateAsync(
            new TrainingPlanStructureInput([first, second]),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors,
            error => error.ErrorCode == "training_day_duplicate");
    }

    [Fact]
    public async Task Structure_DuplicateSetNumber_IsRejected()
    {
        var exercise = new TrainingPlanStructureInput.DayExerciseInput(
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            null,
            null,
            null,
            [Set(Guid.NewGuid(), 1), Set(Guid.NewGuid(), 1)]);
        var day = new TrainingPlanStructureInput.TrainingDayInput(
            Guid.NewGuid(), 1, 1, null, [exercise]);
        var validator = new TrainingPlanStructureValidator(false);

        var result = await validator.ValidateAsync(
            new TrainingPlanStructureInput([day]),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors,
            error => error.ErrorCode == "training_set_number_duplicate");
    }

    [Fact]
    public async Task Structure_NullDays_ReturnsValidationError()
    {
        var validator = new TrainingPlanStructureValidator(false);

        var result = await validator.ValidateAsync(
            new TrainingPlanStructureInput(null!),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors,
            error => error.ErrorCode == "training_days_required");
    }

    [Fact]
    public async Task Structure_NullExercises_ReturnsValidationError()
    {
        var validator = new TrainingPlanStructureValidator(false);
        var day = new TrainingPlanStructureInput.TrainingDayInput(
            Guid.NewGuid(), 1, 1, null, null!);

        var result = await validator.ValidateAsync(
            new TrainingPlanStructureInput([day]),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors,
            error => error.ErrorCode == "training_day_exercises_required");
    }

    [Fact]
    public async Task Structure_NullSets_ReturnsValidationError()
    {
        var validator = new TrainingPlanStructureValidator(false);
        var exercise = new TrainingPlanStructureInput.DayExerciseInput(
            Guid.NewGuid(), Guid.NewGuid(), 1, null, null, null, null!);
        var day = new TrainingPlanStructureInput.TrainingDayInput(
            Guid.NewGuid(), 1, 1, null, [exercise]);

        var result = await validator.ValidateAsync(
            new TrainingPlanStructureInput([day]),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors,
            error => error.ErrorCode == "training_exercise_sets_required");
    }

    [Fact]
    public async Task Record_PerformedAtMissing_IsRejected()
    {
        var validator = new RecordExerciseSetLogCommandValidator();
        var command = new RecordExerciseSetLogCommand(
            Guid.NewGuid(), 1, 0m, 0, null, default);

        var result = await validator.ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors,
            error => error.ErrorCode == "training_performed_at_required");
    }

    [Fact]
    public async Task Record_ZeroReps_IsAccepted()
    {
        var validator = new RecordExerciseSetLogCommandValidator();
        var command = new RecordExerciseSetLogCommand(
            Guid.NewGuid(), 1, 0m, 0, null, DateTimeOffset.UtcNow);

        var result = await validator.ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(result.Errors,
            error => error.ErrorCode == "training_reps_done_invalid");
    }

    [Fact]
    public async Task Correct_ZeroReps_IsAccepted()
    {
        var validator = new CorrectExerciseSetLogCommandValidator();
        var command = new CorrectExerciseSetLogCommand(
            Guid.NewGuid(), 0m, 0, null, DateTimeOffset.UtcNow);

        var result = await validator.ValidateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(result.Errors,
            error => error.ErrorCode == "training_reps_done_invalid");
    }

    [Fact]
    public async Task List_PerformedFromAfterPerformedTo_IsRejected()
    {
        var validator = new ListExerciseSetLogsQueryValidator();
        var from = new DateTimeOffset(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);
        var query = new ListExerciseSetLogsQuery(
            Guid.NewGuid(), null, from, from.AddMinutes(-1));

        var result = await validator.ValidateAsync(
            query,
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors,
            error => error.ErrorCode == "training_log_date_range_invalid");
    }

    private static TrainingPlanStructureInput Structure(Guid? dayId) =>
        new([Day(dayId, 1, 1)]);

    private static TrainingPlanStructureInput.TrainingDayInput Day(
        Guid? id,
        int week,
        int weekday) => new(
            id,
            weekday,
            week,
            null,
            [new TrainingPlanStructureInput.DayExerciseInput(
                id.HasValue ? Guid.NewGuid() : null,
                Guid.NewGuid(),
                1,
                null,
                null,
                null,
                [Set(id.HasValue ? Guid.NewGuid() : null, 1)])]);

    private static TrainingPlanStructureInput.ExerciseSetInput Set(
        Guid? id,
        int number) => new(id, number, 10, 20m, 60, 90);
}
