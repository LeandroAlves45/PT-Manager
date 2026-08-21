using Application.Common.Abstractions;
using Application.Features.Training.ExerciseSetLogs.Abstractions;
using Application.Features.Training.ExerciseSetLogs.CorrectExerciseSetLog;
using Application.Features.Training.ExerciseSetLogs.Dtos;
using Application.Features.Training.ExerciseSetLogs.ListExerciseSetLogs;
using Application.Features.Training.ExerciseSetLogs.RecordExerciseSetLog;
using Application.Pagination;
using Domain.Entities.Training;

namespace Application.UnitTests.Features.Training;

public sealed class ExerciseSetLogHandlersTests
{
    private static readonly Guid TrainerId = Guid.NewGuid();
    private static readonly Guid ClientId = Guid.NewGuid();
    private static readonly DateTime Now =
        new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Record_ValidCommand_UsesEffectiveTenant()
    {
        var store = new FakeExerciseSetLogStore
        {
            RecordResult = ExerciseSetLogStoreResult.ForRecorded(CreateLog())
        };
        var queries = new FakeExerciseSetLogQueries { Details = CreateDto() };
        var handler = new RecordExerciseSetLogHandler(
            new RecordExerciseSetLogCommandValidator(),
            new TenantStub(TrainerId),
            new ClockStub(Now),
            store,
            queries);

        var result = await handler.HandleAsync(ValidRecordCommand(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(TrainerId, store.LastTrainerId);
    }

    [Fact]
    public async Task Record_WrongRole_ReturnsForbiddenWithoutWriting()
    {
        var store = new FakeExerciseSetLogStore();
        var handler = new RecordExerciseSetLogHandler(
            new RecordExerciseSetLogCommandValidator(),
            new TenantStub(TrainerId, role: "client"),
            new ClockStub(Now),
            store,
            new FakeExerciseSetLogQueries());

        var result = await handler.HandleAsync(ValidRecordCommand(), TestContext.Current.CancellationToken);

        Assert.Equal("exercise_set_log_trainer_only", result.Error!.Code);
        Assert.Equal(Application.Errors.ErrorCategory.Forbidden, result.Error.Category);
        Assert.Equal(0, store.RecordCalls);
    }

    [Fact]
    public async Task Correct_ValidCommand_UsesEffectiveTenant()
    {
        var store = new FakeExerciseSetLogStore
        {
            CorrectResult = ExerciseSetLogStoreResult.ForCorrected(CreateLog())
        };
        var queries = new FakeExerciseSetLogQueries { Details = CreateDto() };
        var handler = new CorrectExerciseSetLogHandler(
            new CorrectExerciseSetLogCommandValidator(),
            new TenantStub(TrainerId),
            new ClockStub(Now),
            store,
            queries);

        var result = await handler.HandleAsync(ValidCorrectCommand(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(TrainerId, store.LastTrainerId);
    }

    [Fact]
    public async Task Correct_WrongRole_ReturnsForbiddenWithoutWriting()
    {
        var store = new FakeExerciseSetLogStore();
        var handler = new CorrectExerciseSetLogHandler(
            new CorrectExerciseSetLogCommandValidator(),
            new TenantStub(TrainerId, role: "client"),
            new ClockStub(Now),
            store,
            new FakeExerciseSetLogQueries());

        var result = await handler.HandleAsync(ValidCorrectCommand(), TestContext.Current.CancellationToken);

        Assert.Equal("exercise_set_log_trainer_only", result.Error!.Code);
        Assert.Equal(Application.Errors.ErrorCategory.Forbidden, result.Error.Category);
        Assert.Equal(0, store.CorrectCalls);
    }

    [Fact]
    public async Task List_ValidQuery_UsesEffectiveTenant()
    {
        var queries = new FakeExerciseSetLogQueries();
        var handler = new ListExerciseSetLogsHandler(
            new ListExerciseSetLogsQueryValidator(),
            new TenantStub(TrainerId),
            queries);

        var result = await handler.HandleAsync(
            new ListExerciseSetLogsQuery(ClientId),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, queries.ListCalls);
    }

    [Fact]
    public async Task List_WrongRole_ReturnsForbiddenWithoutQuerying()
    {
        var queries = new FakeExerciseSetLogQueries();
        var handler = new ListExerciseSetLogsHandler(
            new ListExerciseSetLogsQueryValidator(),
            new TenantStub(TrainerId, role: "client"),
            queries);

        var result = await handler.HandleAsync(
            new ListExerciseSetLogsQuery(ClientId),
            TestContext.Current.CancellationToken);

        Assert.Equal("exercise_set_log_trainer_only", result.Error!.Code);
        Assert.Equal(Application.Errors.ErrorCategory.Forbidden, result.Error.Category);
        Assert.Equal(0, queries.ListCalls);
    }

    private static RecordExerciseSetLogCommand ValidRecordCommand() => new(
        Guid.NewGuid(), 1, 50m, 10, null, new DateTimeOffset(Now));

    private static CorrectExerciseSetLogCommand ValidCorrectCommand() => new(
        Guid.NewGuid(), 55m, 8, null, new DateTimeOffset(Now));

    private static ClientExerciseSetLog CreateLog() => new(
        ClientId, Guid.NewGuid(), 1, 50m, 10, null, new DateTimeOffset(Now), Now);

    private static ClientExerciseSetLogDto CreateDto() => new(
        Guid.NewGuid(), ClientId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), "Squat", 1, 50m, 10, null, new DateTimeOffset(Now), Now, Now);

    private sealed class FakeExerciseSetLogStore : IExerciseSetLogStore
    {
        public ExerciseSetLogStoreResult RecordResult { get; init; } =
            ExerciseSetLogStoreResult.ForNotFound();
        public ExerciseSetLogStoreResult CorrectResult { get; init; } =
            ExerciseSetLogStoreResult.ForNotFound();
        public int RecordCalls { get; private set; }
        public int CorrectCalls { get; private set; }
        public Guid LastTrainerId { get; private set; }

        public Task<ExerciseSetLogStoreResult> RecordAsync(
            Guid trainerId,
            RecordExerciseSetLogWriteModel model,
            DateTimeOffset currentInstant,
            DateTime now,
            CancellationToken cancellationToken)
        {
            RecordCalls++;
            LastTrainerId = trainerId;
            return Task.FromResult(RecordResult);
        }

        public Task<ExerciseSetLogStoreResult> CorrectAsync(
            Guid trainerId,
            CorrectExerciseSetLogWriteModel model,
            DateTimeOffset currentInstant,
            DateTime now,
            CancellationToken cancellationToken)
        {
            CorrectCalls++;
            LastTrainerId = trainerId;
            return Task.FromResult(CorrectResult);
        }
    }

    private sealed class FakeExerciseSetLogQueries : IExerciseSetLogQueries
    {
        public ClientExerciseSetLogDto? Details { get; init; }
        public int ListCalls { get; private set; }

        public Task<ClientExerciseSetLogDto?> GetAsync(
            Guid exerciseSetLogId,
            CancellationToken cancellationToken) => Task.FromResult(Details);

        public Task<PageResult<ClientExerciseSetLogDto>> ListAsync(
            Guid clientId,
            Guid? trainingPlanId,
            DateTimeOffset? performedFrom,
            DateTimeOffset? performedTo,
            PageRequest page,
            CancellationToken cancellationToken)
        {
            ListCalls++;
            return Task.FromResult(new PageResult<ClientExerciseSetLogDto>([], 0));
        }
    }

    private sealed class ClockStub(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class TenantStub(
        Guid trainerId,
        Guid? userId = null,
        string? role = "trainer"
    ) : ITenantContext
    {
        public Guid? TrainerId { get; } = trainerId;
        public Guid? UserId { get; } = userId ?? Guid.NewGuid();
        public string? Role { get; } = role;
        public TenantOrigin Origin => TenantOrigin.Http;
        public bool IsAdministrative => false;
    }
}
