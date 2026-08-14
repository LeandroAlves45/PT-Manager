using Application.Features.Training.TrainingPlans;
using Application.Features.Training.TrainingPlans.Abstractions;
using Domain.Entities.Training;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Training;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Training;

[Collection(PostgresCollection.Name)]
public sealed class TrainingPlanStoreTests
{
    private static readonly DateTime Now =
        new(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);
    private readonly PostgresContainerFixture _fixture;

    public TrainingPlanStoreTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task UpdateStructure_FiveDaysToFourWithoutLogs_PreservesKeptIds()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(dayCount: 5, token);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var original = await LoadAsync(context, seed.PlanId, token);
        var removedId = original.Days.OrderBy(day => day.DayOfWeek).Last().Id;
        var keptIds = original.Days.Where(day => day.Id != removedId)
            .Select(day => day.Id).ToHashSet();
        var structure = ToInput(original, day => day.Id != removedId);
        var store = new TrainingPlanStore(
            context,
            new TrainingPlanStructureCoordinator(context));

        var result = await store.UpdateStructureAsync(
            seed.TrainerId,
            new UpdateTrainingPlanStructureWriteModel(seed.PlanId, structure),
            Now.AddHours(1),
            token);

        Assert.Equal(TrainingPlanStoreResult.Status.Updated, result.Kind);
        context.ChangeTracker.Clear();
        var persisted = await LoadAsync(context, seed.PlanId, token);
        Assert.Equal(4, persisted.Days.Count);
        Assert.True(keptIds.SetEquals(persisted.Days.Select(day => day.Id)));
    }

    [Fact]
    public async Task UpdateStructure_DestructiveChangeWithLog_ReturnsHistoryConflict()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(dayCount: 1, token, withLog: true);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var plan = await LoadAsync(context, seed.PlanId, token);
        var empty = new TrainingPlanStructureInput([]);
        var store = new TrainingPlanStore(
            context,
            new TrainingPlanStructureCoordinator(context));

        var result = await store.UpdateStructureAsync(
            seed.TrainerId,
            new UpdateTrainingPlanStructureWriteModel(plan.Id, empty),
            Now.AddHours(1),
            token);

        Assert.Equal(TrainingPlanStoreResult.Status.StructureHasHistory, result.Kind);
        context.ChangeTracker.Clear();
        Assert.Single((await LoadAsync(context, seed.PlanId, token)).Days);
    }

    [Fact]
    public async Task Replace_WithExistingHistory_ArchivesOldAndCreatesOnlyNewIds()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(dayCount: 1, token, withLog: true);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var current = await LoadAsync(context, seed.PlanId, token);
        var oldIds = StructuralIds(current);
        var structure = NewStructure(seed.ExerciseId, dayCount: 1);
        var store = new TrainingPlanStore(
            context,
            new TrainingPlanStructureCoordinator(context));

        var result = await store.ReplaceAsync(
            seed.TrainerId,
            new ReplaceTrainingPlanWriteModel(
                current.Id,
                "Replacement",
                null,
                null,
                null,
                new DateOnly(2026, 8, 1),
                null,
                structure),
            Now.AddHours(1),
            token);

        Assert.Equal(TrainingPlanStoreResult.Status.Replaced, result.Kind);
        context.ChangeTracker.Clear();
        var oldPlan = await context.TrainingPlans.AsNoTracking()
            .SingleAsync(plan => plan.Id == seed.PlanId, token);
        var replacement = await LoadAsync(context, result.TrainingPlanId!.Value, token);
        Assert.True(oldPlan.IsArchived);
        Assert.False(oldPlan.IsActive);
        Assert.Empty(oldIds.Intersect(StructuralIds(replacement)));
        Assert.Equal(seed.ExerciseId,
            Assert.Single(Assert.Single(replacement.Days).Exercises).ExerciseId);
        Assert.Single(await context.ClientExerciseSetLogs.AsNoTracking().ToListAsync(token));
    }

    [Fact]
    public async Task Replace_FailureDuringNewInsert_RollsBackOldArchive()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(dayCount: 1, token);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = new TrainingPlanStore(
            context,
            new TrainingPlanStructureCoordinator(context));
        var invalid = NewStructure(seed.ExerciseId, dayCount: 1) with
        {
            Days = [
                NewStructure(seed.ExerciseId, 1).Days[0],
                NewStructure(seed.ExerciseId, 1).Days[0]
            ]
        };

        await Assert.ThrowsAnyAsync<Exception>(() => store.ReplaceAsync(
            seed.TrainerId,
            new ReplaceTrainingPlanWriteModel(
                seed.PlanId,
                "Invalid replacement",
                null,
                null,
                null,
                new DateOnly(2026, 8, 1),
                null,
                invalid),
            Now.AddHours(1),
            token));

        context.ChangeTracker.Clear();
        var oldPlan = await context.TrainingPlans.AsNoTracking()
            .SingleAsync(plan => plan.Id == seed.PlanId, token);
        Assert.True(oldPlan.IsActive);
        Assert.False(oldPlan.IsArchived);
    }

    private async Task<Seed> SeedAsync(
        int dayCount,
        CancellationToken token,
        bool withLog = false)
    {
        var tenant = await _fixture.SeedTenantWithClientAsync(
            $"training-{Guid.NewGuid():N}", token);
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        var exercise = new Exercise(
            tenant.TrainerId,
            "Back squat",
            "Legs",
            null,
            null,
            null,
            null,
            Now);
        var plan = new TrainingPlan(
            tenant.TrainerId,
            tenant.ClientId,
            "Initial plan",
            null,
            null,
            null,
            new DateOnly(2026, 8, 1),
            null,
            Now);
        for (var index = 0; index < dayCount; index++)
        {
            var prescribed = plan.AddDay(index, 1, null, Now)
                .AddExercise(exercise.Id, 1, null, null, null, Now);
            prescribed.AddSet(1, 10, 40m, 60, 90, Now);
        }

        context.Exercises.Add(exercise);
        context.TrainingPlans.Add(plan);
        await context.SaveChangesAsync(token);
        if (withLog)
        {
            var prescribed = Assert.Single(plan.Days).Exercises.Single();
            context.ClientExerciseSetLogs.Add(new ClientExerciseSetLog(
                tenant.ClientId,
                prescribed.Id,
                1,
                40m,
                10,
                null,
                new DateTimeOffset(Now),
                Now));
            await context.SaveChangesAsync(token);
        }

        return new Seed(tenant.TrainerId, plan.Id, exercise.Id);
    }

    private static Task<TrainingPlan> LoadAsync(
        Infrastructure.Data.PtManagerDbContext context,
        Guid id,
        CancellationToken token) => context.TrainingPlans
            .Include(plan => plan.Days)
                .ThenInclude(day => day.Exercises)
                    .ThenInclude(item => item.Sets)
            .AsSplitQuery()
            .SingleAsync(plan => plan.Id == id, token);

    private static TrainingPlanStructureInput ToInput(
        TrainingPlan plan,
        Func<TrainingPlanDay, bool> include) => new(
            plan.Days.Where(include).Select(day =>
                new TrainingPlanStructureInput.TrainingDayInput(
                    day.Id,
                    day.DayOfWeek,
                    day.WeekNumber,
                    day.Notes,
                    day.Exercises.Select(item =>
                        new TrainingPlanStructureInput.DayExerciseInput(
                            item.Id,
                            item.ExerciseId,
                            item.OrderNumber,
                            item.ExerciseGroupId,
                            item.GroupPosition,
                            item.Notes,
                            item.Sets.Select(set =>
                                new TrainingPlanStructureInput.ExerciseSetInput(
                                    set.Id,
                                    set.SetNumber,
                                    set.PlannedReps,
                                    set.PlannedWeightKg,
                                    set.RestSecondsMin,
                                    set.RestSecondsMax)).ToArray())).ToArray())).ToArray());

    private static TrainingPlanStructureInput NewStructure(Guid exerciseId, int dayCount) =>
        new(Enumerable.Range(0, dayCount).Select(index =>
            new TrainingPlanStructureInput.TrainingDayInput(
                null,
                index,
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
                        null, 1, 10, 40m, 60, 90)])])).ToArray());

    private static HashSet<Guid> StructuralIds(TrainingPlan plan) =>
        plan.Days.Select(day => day.Id)
            .Concat(plan.Days.SelectMany(day => day.Exercises).Select(item => item.Id))
            .Concat(plan.Days.SelectMany(day => day.Exercises)
                .SelectMany(item => item.Sets).Select(set => set.Id))
            .ToHashSet();

    private sealed record Seed(Guid TrainerId, Guid PlanId, Guid ExerciseId);
}
