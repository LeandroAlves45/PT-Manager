using Application.Features.Training.ExerciseSetLogs.Abstractions;
using Application.Features.Training.TrainingPlans;
using Application.Features.Training.TrainingPlans.Abstractions;
using Application.Features.Training.TrainingPlans.ListTrainingPlans;
using Application.Pagination;
using Domain.Entities.Training;
using Infrastructure.Data;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Training;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Training;

[Collection(PostgresCollection.Name)]
public sealed class TrainingConcurrencyTests
{
    private static readonly DateTime Now =
        new(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);
    private readonly PostgresContainerFixture _fixture;

    public TrainingConcurrencyTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Create_ConcurrentForSameClient_CreatesOneActivePlan()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedCatalogAsync(token);
        await using var firstContext = _fixture.CreateContext(seed.TrainerId);
        await using var secondContext = _fixture.CreateContext(seed.TrainerId);
        var firstStore = CreatePlanStore(firstContext);
        var secondStore = CreatePlanStore(secondContext);

        var results = await Task.WhenAll(
            firstStore.CreateAsync(
                seed.TrainerId,
                CreateModel(seed.ClientId, seed.ExerciseId, "Plan A"),
                Now,
                token),
            secondStore.CreateAsync(
                seed.TrainerId,
                CreateModel(seed.ClientId, seed.ExerciseId, "Plan B"),
                Now,
                token));

        Assert.Equal(
            [TrainingPlanStoreResult.Status.Created, TrainingPlanStoreResult.Status.ActivePlanConflict],
            results.Select(result => result.Kind).Order().ToArray());
    }

    [Fact]
    public async Task Record_ConcurrentWithDestructiveUpdate_ProducesSerializableOutcome()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedPlanAsync(token);
        await using var logContext = _fixture.CreateContext(seed.TrainerId);
        await using var planContext = _fixture.CreateContext(seed.TrainerId);
        var logStore = new ExerciseSetLogStore(logContext);
        var planStore = CreatePlanStore(planContext);

        var results = await Task.WhenAll(
            RecordAsync(logStore, seed, token),
            UpdateToEmptyAsync(planStore, seed, token));

        var recordStatus = (ExerciseSetLogStoreResult.Status)results[0];
        var updateStatus = (TrainingPlanStoreResult.Status)results[1];
        var isValid =
            recordStatus == ExerciseSetLogStoreResult.Status.Recorded &&
            updateStatus == TrainingPlanStoreResult.Status.StructureHasHistory ||
            recordStatus == ExerciseSetLogStoreResult.Status.NotFound &&
            updateStatus == TrainingPlanStoreResult.Status.Updated;

        Assert.True(isValid);
    }

    [Fact]
    public async Task Replace_ConcurrentWithRecord_DoesNotWriteAfterArchiveWins()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedPlanAsync(token);
        await using var logContext = _fixture.CreateContext(seed.TrainerId);
        await using var planContext = _fixture.CreateContext(seed.TrainerId);
        var logStore = new ExerciseSetLogStore(logContext);
        var planStore = CreatePlanStore(planContext);

        var recordTask = logStore.RecordAsync(
            seed.TrainerId,
            LogModel(seed.DayExerciseId),
            new DateTimeOffset(Now),
            Now,
            token);
        var replaceTask = planStore.ReplaceAsync(
            seed.TrainerId,
            new ReplaceTrainingPlanWriteModel(
                seed.PlanId,
                "Replacement",
                null,
                null,
                null,
                new DateOnly(2026, 8, 1),
                null,
                NewStructure(seed.ExerciseId)),
            Now.AddMinutes(1),
            token);

        await Task.WhenAll(recordTask, replaceTask);

        var recordResult = await recordTask;
        var replaceResult = await replaceTask;
        var allowedRecordStatuses = new[]
        {
            ExerciseSetLogStoreResult.Status.Recorded,
            ExerciseSetLogStoreResult.Status.TrainingPlanInactive,
            ExerciseSetLogStoreResult.Status.NotFound
        };

        Assert.Equal(TrainingPlanStoreResult.Status.Replaced, replaceResult.Kind);
        Assert.Contains(
            recordResult.Kind,
            allowedRecordStatuses);
    }

    [Fact]
    public async Task Operations_OtherTenant_ReturnSafeNotFoundOrEmpty()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedPlanAsync(token, withLog: true);
        var other = await _fixture.SeedTenantWithClientAsync(
            $"training-other-{Guid.NewGuid():N}", token);
        await using var context = _fixture.CreateContext(other.TrainerId);
        var planStore = CreatePlanStore(context);
        var logStore = new ExerciseSetLogStore(context);
        var queries = new ExerciseSetLogQueries(context);
        var planQueries = new TrainingPlanQueries(context);

        var update = await planStore.UpdateStructureAsync(
            other.TrainerId,
            new UpdateTrainingPlanStructureWriteModel(seed.PlanId, new([])),
            Now,
            token);
        var archive = await planStore.ArchiveAsync(
            seed.PlanId, other.TrainerId, Now, token);
        var replace = await planStore.ReplaceAsync(
            other.TrainerId,
            new ReplaceTrainingPlanWriteModel(
                seed.PlanId,
                "Replacement",
                null,
                null,
                null,
                new DateOnly(2026, 8, 1),
                null,
                NewStructure(seed.ExerciseId)),
            Now,
            token);
        var record = await logStore.RecordAsync(
            other.TrainerId,
            LogModel(seed.DayExerciseId),
            new DateTimeOffset(Now),
            Now,
            token);
        var correct = await logStore.CorrectAsync(
            other.TrainerId,
            new CorrectExerciseSetLogWriteModel(
                seed.LogId!.Value,
                45m,
                8,
                null,
                new DateTimeOffset(Now.AddMinutes(-1))),
            new DateTimeOffset(Now),
            Now,
            token);
        var get = await queries.GetAsync(seed.LogId.Value, token);
        var list = await queries.ListAsync(
            seed.ClientId,
            null,
            null,
            null,
            new PageRequest(1, 20),
            token);
        var planDetails = await planQueries.GetDetailsAsync(seed.PlanId, token);
        var plans = await planQueries.ListAsync(
            seed.ClientId,
            null,
            TrainingPlanActivityFilter.All,
            new PageRequest(1, 20),
            token);

        Assert.Equal(
            (
                TrainingPlanStoreResult.Status.NotFound,
                TrainingPlanStoreResult.Status.NotFound,
                TrainingPlanStoreResult.Status.NotFound,
                ExerciseSetLogStoreResult.Status.NotFound,
                ExerciseSetLogStoreResult.Status.NotFound,
                true,
                0,
                true,
                0),
            (
                update.Kind,
                archive.Kind,
                replace.Kind,
                record.Kind,
                correct.Kind,
                get is null,
                list.TotalCount,
                planDetails is null,
                plans.TotalCount));
    }

    [Fact]
    public async Task UpdateStructure_DayAndSetSwap_PreservesIdentifiers()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedPlanAsync(token, dayCount: 2, setCount: 2);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var plan = await LoadAsync(context, seed.PlanId, token);
        var days = plan.Days.OrderBy(day => day.DayOfWeek).ToArray();
        var firstExercise = days[0].Exercises.Single();
        var sets = firstExercise.Sets.OrderBy(set => set.SetNumber).ToArray();
        var structure = ToInput(plan);
        structure = structure with
        {
            Days = structure.Days.Select(day => day.Id switch
            {
                var id when id == days[0].Id => day with
                {
                    DayOfWeek = days[1].DayOfWeek,
                    Exercises = day.Exercises.Select(exercise => exercise with
                    {
                        Sets = exercise.Sets.Select(set => set.Id switch
                        {
                            var id when id == sets[0].Id => set with { SetNumber = 2 },
                            var id when id == sets[1].Id => set with { SetNumber = 1 },
                            _ => set
                        }).ToArray()
                    }).ToArray()
                },
                var id when id == days[1].Id => day with { DayOfWeek = days[0].DayOfWeek },
                _ => day
            }).ToArray()
        };

        var result = await CreatePlanStore(context).UpdateStructureAsync(
            seed.TrainerId,
            new UpdateTrainingPlanStructureWriteModel(seed.PlanId, structure),
            Now.AddMinutes(1),
            token);

        context.ChangeTracker.Clear();
        var persisted = await LoadAsync(context, seed.PlanId, token);
        Assert.Equal(TrainingPlanStoreResult.Status.Updated, result.Kind);
        Assert.Equal(days[1].DayOfWeek, persisted.GetDay(days[0].Id).DayOfWeek);
        Assert.Equal(days[0].DayOfWeek, persisted.GetDay(days[1].Id).DayOfWeek);
        var persistedExercise = persisted.GetDay(days[0].Id).GetExercise(firstExercise.Id);
        Assert.Equal(2, persistedExercise.GetSet(sets[0].Id).SetNumber);
        Assert.Equal(1, persistedExercise.GetSet(sets[1].Id).SetNumber);
    }

    [Fact]
    public async Task UpdateStructure_AllSetSlotsOccupied_ReturnsTypedConflict()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedPlanAsync(token, setCount: 15);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var plan = await LoadAsync(context, seed.PlanId, token);
        var exercise = plan.Days.Single().Exercises.Single();
        var sets = exercise.Sets.OrderBy(set => set.SetNumber).ToArray();
        var structure = ToInput(plan);
        structure = structure with
        {
            Days = structure.Days.Select(day => day with
            {
                Exercises = day.Exercises.Select(item => item with
                {
                    Sets = item.Sets.Select(set => set.Id switch
                    {
                        var id when id == sets[0].Id => set with { SetNumber = 2 },
                        var id when id == sets[1].Id => set with { SetNumber = 1 },
                        _ => set
                    }).ToArray()
                }).ToArray()
            }).ToArray()
        };

        var result = await CreatePlanStore(context).UpdateStructureAsync(
            seed.TrainerId,
            new UpdateTrainingPlanStructureWriteModel(seed.PlanId, structure),
            Now.AddMinutes(1),
            token);

        Assert.Equal(
            TrainingPlanStoreResult.Status.StructureReorderRequiresFreeSlot,
            result.Kind);
    }

    [Fact]
    public async Task Create_CancelledToken_DoesNotCommitPlan()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedCatalogAsync(token);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = CreatePlanStore(context);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.CreateAsync(
            seed.TrainerId,
            CreateModel(seed.ClientId, seed.ExerciseId, "Cancelled"),
            Now,
            cancellation.Token));

        context.ChangeTracker.Clear();
        Assert.False(await context.TrainingPlans.AnyAsync(
            plan => plan.ClientId == seed.ClientId,
            token));
    }

    private async Task<CatalogSeed> SeedCatalogAsync(CancellationToken token)
    {
        var tenant = await _fixture.SeedTenantWithClientAsync(
            $"training-concurrency-{Guid.NewGuid():N}", token);
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        var exercise = new Exercise(
            tenant.TrainerId,
            "Back squat",
            null,
            "Legs",
            "Barbell",
            null,
            null,
            Now);
        context.Exercises.Add(exercise);
        await context.SaveChangesAsync(token);
        return new CatalogSeed(tenant.TrainerId, tenant.ClientId, exercise.Id);
    }

    private async Task<PlanSeed> SeedPlanAsync(
        CancellationToken token,
        int dayCount = 1,
        int setCount = 1,
        bool withLog = false)
    {
        var catalog = await SeedCatalogAsync(token);
        await using var context = _fixture.CreateContext(catalog.TrainerId);
        var plan = new TrainingPlan(
            catalog.TrainerId,
            catalog.ClientId,
            "Initial plan",
            null,
            null,
            null,
            new DateOnly(2026, 8, 1),
            null,
            Now);
        TrainingPlanDayExercise? firstExercise = null;
        for (var dayIndex = 0; dayIndex < dayCount; dayIndex++)
        {
            var prescribed = plan.AddDay(dayIndex, 1, null, Now)
                .AddExercise(catalog.ExerciseId, 1, null, null, null, Now);
            firstExercise ??= prescribed;
            for (var setNumber = 1; setNumber <= setCount; setNumber++)
                prescribed.AddSet(setNumber, 10, 40m, 60, 90, Now);
        }

        context.TrainingPlans.Add(plan);
        ClientExerciseSetLog? log = null;
        if (withLog)
        {
            log = new ClientExerciseSetLog(
                catalog.ClientId,
                firstExercise!.Id,
                1,
                40m,
                10,
                null,
                new DateTimeOffset(Now.AddMinutes(-5)),
                Now);
            context.ClientExerciseSetLogs.Add(log);
        }
        await context.SaveChangesAsync(token);

        return new PlanSeed(
            catalog.TrainerId,
            catalog.ClientId,
            catalog.ExerciseId,
            plan.Id,
            firstExercise!.Id,
            log?.Id);
    }

    private static TrainingPlanStore CreatePlanStore(PtManagerDbContext context) =>
        new(context, new TrainingPlanStructureCoordinator(context));

    private static CreateTrainingPlanWriteModel CreateModel(
        Guid clientId,
        Guid exerciseId,
        string name) => new(
            clientId,
            name,
            null,
            null,
            null,
            new DateOnly(2026, 8, 1),
            null,
            NewStructure(exerciseId));

    private static TrainingPlanStructureInput NewStructure(Guid exerciseId) => new([
        new TrainingPlanStructureInput.TrainingDayInput(
            null,
            1,
            1,
            null,
            [new TrainingPlanStructureInput.DayExerciseInput(
                null,
                exerciseId,
                1,
                null,
                null,
                null,
                [new TrainingPlanStructureInput.ExerciseSetInput(
                    null, 1, 10, 40m, 60, 90)])])
    ]);

    private static RecordExerciseSetLogWriteModel LogModel(Guid dayExerciseId) => new(
        dayExerciseId,
        1,
        40m,
        10,
        null,
        new DateTimeOffset(Now.AddMinutes(-1)));

    private static async Task<Enum> RecordAsync(
        ExerciseSetLogStore store,
        PlanSeed seed,
        CancellationToken token) =>
        (await store.RecordAsync(
            seed.TrainerId,
            LogModel(seed.DayExerciseId),
            new DateTimeOffset(Now),
            Now,
            token)).Kind;

    private static async Task<Enum> UpdateToEmptyAsync(
        TrainingPlanStore store,
        PlanSeed seed,
        CancellationToken token) =>
        (await store.UpdateStructureAsync(
            seed.TrainerId,
            new UpdateTrainingPlanStructureWriteModel(seed.PlanId, new([])),
            Now,
            token)).Kind;

    private static Task<TrainingPlan> LoadAsync(
        PtManagerDbContext context,
        Guid planId,
        CancellationToken token) => context.TrainingPlans
        .Include(plan => plan.Days)
            .ThenInclude(day => day.Exercises)
                .ThenInclude(exercise => exercise.Sets)
        .AsSplitQuery()
        .SingleAsync(plan => plan.Id == planId, token);

    private static TrainingPlanStructureInput ToInput(TrainingPlan plan) => new(
        plan.Days.Select(day => new TrainingPlanStructureInput.TrainingDayInput(
            day.Id,
            day.DayOfWeek,
            day.WeekNumber,
            day.Notes,
            day.Exercises.Select(exercise =>
                new TrainingPlanStructureInput.DayExerciseInput(
                    exercise.Id,
                    exercise.ExerciseId,
                    exercise.OrderNumber,
                    exercise.ExerciseGroupId,
                    exercise.GroupPosition,
                    exercise.Notes,
                    exercise.Sets.Select(set =>
                        new TrainingPlanStructureInput.ExerciseSetInput(
                            set.Id,
                            set.SetNumber,
                            set.PlannedReps,
                            set.PlannedWeightKg,
                            set.RestSecondsMin,
                            set.RestSecondsMax)).ToArray())).ToArray())).ToArray());

    private sealed record CatalogSeed(Guid TrainerId, Guid ClientId, Guid ExerciseId);

    private sealed record PlanSeed(
        Guid TrainerId,
        Guid ClientId,
        Guid ExerciseId,
        Guid PlanId,
        Guid DayExerciseId,
        Guid? LogId);
}
