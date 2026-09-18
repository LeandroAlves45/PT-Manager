using Application.Common.Abstractions;
using Application.Errors;
using Application.Features.Assessments.CheckIns.Abstractions;
using Application.Features.Assessments.CheckIns.MarkCheckInReviewed;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.MarkMySupplementIntake;
using Application.Features.Supplements.UnmarkMySupplementIntake;
using Application.Features.Training.ExerciseSetLogs.Abstractions;
using Application.Features.Training.ExerciseSetLogs.CorrectMyExerciseSetLog;
using Application.Features.Training.ExerciseSetLogs.DeleteMyExerciseSetLog;
using Application.Features.Training.ExerciseSetLogs.RecordMyExerciseSetLog;
using Application.Features.Training.WorkoutCompletions.Abstractions;
using Application.Features.Training.WorkoutCompletions.CompleteMyWorkout;
using Domain.Entities.Supplements;
using Domain.Entities.Training;
using Domain.ValueObjects;

namespace Application.UnitTests.Features;

/// <summary>
/// Prova autorização, identidade efetiva e mapeamento de resultados dos casos de uso do
/// portal e da revisão de check-in (Sprint 6A).
/// </summary>
public sealed class Sprint6AClientHandlersTests
{
    private static readonly DateTime Now = new(2026, 9, 16, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TrainerId = Guid.NewGuid();
    private static readonly Guid ClientUserId = Guid.NewGuid();

    [Fact]
    public async Task RecordMySet_WithTrainerToken_ReturnsForbiddenWithoutCallingStore()
    {
        var store = new SetLogStoreFake();
        var handler = new RecordMyExerciseSetLogHandler(
            new RecordMyExerciseSetLogCommandValidator(), Tenant("trainer"), new ClockStub(), store);

        var result = await handler.HandleAsync(ValidRecord(), CancellationToken.None);

        Assert.Equal("training_client_only", result.Error!.Code);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task RecordMySet_WithClientToken_UsesIdentityFromTenantContext()
    {
        var log = new ClientExerciseSetLog(Guid.NewGuid(), Guid.NewGuid(), 1, 50m, 8, null, new DateTimeOffset(Now), Now, 8m);
        var store = new SetLogStoreFake { Outcome = MyExerciseSetLogStoreResult.ForRecorded(log) };
        var handler = new RecordMyExerciseSetLogHandler(
            new RecordMyExerciseSetLogCommandValidator(), Tenant("client"), new ClockStub(), store);

        var result = await handler.HandleAsync(ValidRecord(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((TrainerId, ClientUserId), (store.TrainerId, store.UserId));
        Assert.Equal(8m, result.Value.Rpe);
    }

    [Theory]
    [InlineData(MyExerciseSetLogStoreResult.Status.NotEditable, "exercise_set_log_not_editable", ErrorCategory.Conflict)]
    [InlineData(MyExerciseSetLogStoreResult.Status.WorkoutAlreadyCompleted, "workout_already_completed", ErrorCategory.Conflict)]
    [InlineData(MyExerciseSetLogStoreResult.Status.NotFound, "exercise_set_log_not_found", ErrorCategory.NotFound)]
    [InlineData(MyExerciseSetLogStoreResult.Status.TrainingPlanInactive, "training_plan_inactive", ErrorCategory.Conflict)]
    public async Task DeleteMySet_MapsStoreFailures(
        MyExerciseSetLogStoreResult.Status status,
        string code,
        ErrorCategory category)
    {
        var store = new SetLogStoreFake { Outcome = MyExerciseSetLogStoreResult.For(status) };
        var handler = new DeleteMyExerciseSetLogHandler(Tenant("client"), new ClockStub(), store);

        var result = await handler.HandleAsync(new DeleteMyExerciseSetLogCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal((code, category), (result.Error!.Code, result.Error.Category));
    }

    [Fact]
    public async Task CorrectMySet_WithDateOutsidePlan_ReturnsConflict()
    {
        var store = new SetLogStoreFake
        {
            Outcome = MyExerciseSetLogStoreResult.For(MyExerciseSetLogStoreResult.Status.DateOutsidePlan)
        };
        var handler = new CorrectMyExerciseSetLogHandler(
            new CorrectMyExerciseSetLogCommandValidator(), Tenant("client"), new ClockStub(), store);

        var result = await handler.HandleAsync(
            new CorrectMyExerciseSetLogCommand(Guid.NewGuid(), 50m, 8, null, null), CancellationToken.None);

        Assert.Equal("training_date_outside_plan", result.Error!.Code);
    }

    [Fact]
    public async Task CompleteWorkout_WhenAlreadyCompleted_ReturnsExistingCompletion()
    {
        var existing = new WorkoutCompletion(TrainerId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 16), null, Now);
        var store = new CompletionStoreFake { Outcome = WorkoutCompletionStoreResult.ForAlreadyCompleted(existing) };
        var handler = new CompleteMyWorkoutHandler(
            new CompleteMyWorkoutCommandValidator(), Tenant("client"), new ClockStub(), store);

        var result = await handler.HandleAsync(
            new CompleteMyWorkoutCommand(existing.TrainingPlanDayId, null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(existing.Id, result.Value.Id);
    }

    [Fact]
    public async Task CompleteWorkout_WithTrainerToken_ReturnsForbidden()
    {
        var store = new CompletionStoreFake();
        var handler = new CompleteMyWorkoutHandler(
            new CompleteMyWorkoutCommandValidator(), Tenant("trainer"), new ClockStub(), store);

        var result = await handler.HandleAsync(
            new CompleteMyWorkoutCommand(Guid.NewGuid(), null), CancellationToken.None);

        Assert.Equal("training_client_only", result.Error!.Code);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task MarkIntake_WhenAssignmentInactive_ReturnsConflict()
    {
        var store = new IntakeStoreFake
        {
            Outcome = SupplementIntakeStoreResult.For(SupplementIntakeStoreResult.Status.AssignmentInactive)
        };
        var handler = new MarkMySupplementIntakeHandler(Tenant("client"), new ClockStub(), store);

        var result = await handler.HandleAsync(new MarkMySupplementIntakeCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(("supplement_assignment_inactive", ErrorCategory.Conflict), (result.Error!.Code, result.Error.Category));
    }

    [Fact]
    public async Task MarkIntake_WhenAlreadyMarked_ReturnsSameIntake()
    {
        var intake = new ClientSupplementIntake(TrainerId, Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2026, 9, 16), Now);
        var store = new IntakeStoreFake { Outcome = SupplementIntakeStoreResult.ForAlreadyMarked(intake) };
        var handler = new MarkMySupplementIntakeHandler(Tenant("client"), new ClockStub(), store);

        var result = await handler.HandleAsync(
            new MarkMySupplementIntakeCommand(intake.ClientSupplementAssignmentId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Now, result.Value.TakenAt);
    }

    [Fact]
    public async Task UnmarkIntake_WhenNothingMarked_Succeeds()
    {
        var store = new IntakeStoreFake
        {
            Outcome = SupplementIntakeStoreResult.For(SupplementIntakeStoreResult.Status.NotMarked)
        };
        var handler = new UnmarkMySupplementIntakeHandler(Tenant("client"), new ClockStub(), store);

        var result = await handler.HandleAsync(new UnmarkMySupplementIntakeCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UnmarkIntake_WithEmptyAssignment_ReturnsValidationWithoutCallingStore()
    {
        var store = new IntakeStoreFake();
        var handler = new UnmarkMySupplementIntakeHandler(Tenant("client"), new ClockStub(), store);

        var result = await handler.HandleAsync(new UnmarkMySupplementIntakeCommand(Guid.Empty), CancellationToken.None);

        Assert.Equal(ErrorCategory.Validation, result.Error!.Category);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task MarkCheckInReviewed_WithClientToken_ReturnsForbidden()
    {
        var handler = new MarkCheckInReviewedHandler(
            Tenant("client"), new ClockStub(), new TimeZoneStub(), new CheckInStoreFake());

        var result = await handler.HandleAsync(new MarkCheckInReviewedCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal("assessment_trainer_only", result.Error!.Code);
    }

    [Fact]
    public async Task MarkCheckInReviewed_WhenNotAnswered_ReturnsConflict()
    {
        var handler = new MarkCheckInReviewedHandler(
            Tenant("trainer"), new ClockStub(), new TimeZoneStub(), new CheckInStoreFake());

        var result = await handler.HandleAsync(new MarkCheckInReviewedCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(("check_in_not_answered", ErrorCategory.Conflict), (result.Error!.Code, result.Error.Category));
    }

    private static RecordMyExerciseSetLogCommand ValidRecord() =>
        new(Guid.NewGuid(), 1, 50m, 8, 8m, null);

    private static TenantStub Tenant(string role) =>
        new(TrainerId, role == "client" ? ClientUserId : TrainerId, role);

    private sealed class TenantStub(Guid trainerId, Guid userId, string role) : ITenantContext
    {
        public Guid? TrainerId { get; } = trainerId;
        public Guid? UserId { get; } = userId;
        public string? Role { get; } = role;
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative => false;
    }

    private sealed class ClockStub : IClock
    {
        public DateTime UtcNow => Now;
    }

    private sealed class TimeZoneStub : ITrainerTimeZoneProvider
    {
        public Task<TimeZoneInfo> GetRequiredAsync(Guid trainerId, CancellationToken cancellationToken) =>
            Task.FromResult(TimeZoneInfo.Utc);
    }

    private sealed class SetLogStoreFake : IMyExerciseSetLogStore
    {
        public MyExerciseSetLogStoreResult Outcome { get; init; } =
            MyExerciseSetLogStoreResult.For(MyExerciseSetLogStoreResult.Status.NotFound);
        public int Calls { get; private set; }
        public Guid TrainerId { get; private set; }
        public Guid UserId { get; private set; }

        public Task<MyExerciseSetLogStoreResult> RecordAsync(Guid trainerId, Guid clientUserId,
            Guid trainingPlanDayExerciseId, int setNumber, decimal weightKg, int repsDone, decimal? rpe,
            string? notes, DateTime now, CancellationToken cancellationToken) => Track(trainerId, clientUserId);

        public Task<MyExerciseSetLogStoreResult> CorrectAsync(Guid trainerId, Guid clientUserId,
            Guid exerciseSetLogId, decimal weightKg, int repsDone, decimal? rpe, string? notes, DateTime now,
            CancellationToken cancellationToken) => Track(trainerId, clientUserId);

        public Task<MyExerciseSetLogStoreResult> DeleteAsync(Guid trainerId, Guid clientUserId,
            Guid exerciseSetLogId, DateTime now, CancellationToken cancellationToken) => Track(trainerId, clientUserId);

        private Task<MyExerciseSetLogStoreResult> Track(Guid trainerId, Guid userId)
        {
            Calls++;
            (TrainerId, UserId) = (trainerId, userId);
            return Task.FromResult(Outcome);
        }
    }

    private sealed class CompletionStoreFake : IWorkoutCompletionStore
    {
        public WorkoutCompletionStoreResult Outcome { get; init; } =
            WorkoutCompletionStoreResult.For(WorkoutCompletionStoreResult.Status.NotFound);
        public int Calls { get; private set; }

        public Task<WorkoutCompletionStoreResult> CompleteAsync(Guid trainerId, Guid clientUserId,
            Guid trainingPlanDayId, string? notes, DateTime now, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(Outcome);
        }
    }

    private sealed class IntakeStoreFake : ISupplementIntakeStore
    {
        public SupplementIntakeStoreResult Outcome { get; init; } =
            SupplementIntakeStoreResult.For(SupplementIntakeStoreResult.Status.AssignmentNotFound);
        public int Calls { get; private set; }

        public Task<SupplementIntakeStoreResult> MarkAsync(Guid trainerId, Guid clientUserId,
            Guid assignmentId, DateTime now, CancellationToken cancellationToken) => Track();

        public Task<SupplementIntakeStoreResult> UnmarkAsync(Guid trainerId, Guid clientUserId,
            Guid assignmentId, DateTime now, CancellationToken cancellationToken) => Track();

        private Task<SupplementIntakeStoreResult> Track()
        {
            Calls++;
            return Task.FromResult(Outcome);
        }
    }

    private sealed class CheckInStoreFake : ICheckInStore
    {
        private static Task<CheckInStoreResult> NotAnswered() =>
            Task.FromResult(CheckInStoreResult.For(CheckInStoreResult.Status.NotAnswered));

        public Task<CheckInStoreResult> CreateAsync(Guid trainerId, Guid clientId, DateOnly checkInDate,
            DateOnly? targetDate, DateTime now, CancellationToken cancellationToken) => NotAnswered();

        public Task<CheckInStoreResult> RescheduleAsync(Guid trainerId, Guid checkInId, DateOnly checkInDate,
            DateOnly? targetDate, DateTime now, CancellationToken cancellationToken) => NotAnswered();

        public Task<CheckInStoreResult> CancelAsync(Guid trainerId, Guid checkInId, DateTime now,
            CancellationToken cancellationToken) => NotAnswered();

        public Task<CheckInStoreResult> SubmitResponseAsync(Guid trainerId, Guid userId, Guid checkInId,
            decimal weightKg, decimal? bodyFatPercentage, string? notes, BodyMeasurements bodyMeasurements,
            CheckInFeedback feedback, int? trainingAdherenceScore, int? nutritionAdherenceScore, DateTime now,
            CancellationToken cancellationToken) => NotAnswered();

        public Task<CheckInStoreResult> CorrectAsync(Guid trainerId, Guid checkInId, DateOnly? targetDate,
            decimal weightKg, decimal? bodyFatPercentage, string? notes, BodyMeasurements bodyMeasurements,
            CheckInFeedback feedback, int? trainingAdherenceScore, int? nutritionAdherenceScore, DateTime now,
            CancellationToken cancellationToken) => NotAnswered();

        public Task<CheckInStoreResult> MarkReviewedAsync(Guid trainerId, Guid checkInId, DateTime now,
            CancellationToken cancellationToken) => NotAnswered();
    }
}
