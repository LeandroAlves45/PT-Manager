using Application.Features.Training.ExerciseSetLogs.Abstractions;
using Domain.Entities.Training;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Training;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Training;

[Collection(PostgresCollection.Name)]
public sealed class ExerciseSetLogStoreTests
{
    private static readonly DateTime Now =
        new(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);
    private readonly PostgresContainerFixture _fixture;

    public ExerciseSetLogStoreTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Record_SameSetAtDifferentInstants_PersistsBothExecutions()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(token);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = new ExerciseSetLogStore(context);

        var first = await store.RecordAsync(
            seed.TrainerId,
            Model(seed.DayExerciseId, new DateTimeOffset(Now.AddMinutes(-10))),
            new DateTimeOffset(Now),
            Now,
            token);
        var second = await store.RecordAsync(
            seed.TrainerId,
            Model(seed.DayExerciseId, new DateTimeOffset(Now.AddMinutes(-5))),
            new DateTimeOffset(Now),
            Now,
            token);

        Assert.Equal(ExerciseSetLogStoreResult.Status.Recorded, first.Kind);
        Assert.Equal(ExerciseSetLogStoreResult.Status.Recorded, second.Kind);
        Assert.NotEqual(first.Log!.Id, second.Log!.Id);
        Assert.Equal(2, await context.ClientExerciseSetLogs.CountAsync(token));
    }

    [Fact]
    public async Task Record_FutureInstant_RollsBack()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(token);
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var result = await new ExerciseSetLogStore(context).RecordAsync(
            seed.TrainerId,
            Model(seed.DayExerciseId, new DateTimeOffset(Now.AddSeconds(1))),
            new DateTimeOffset(Now),
            Now,
            token);

        Assert.Equal(ExerciseSetLogStoreResult.Status.PerformedAtInFuture, result.Kind);
        Assert.Empty(await context.ClientExerciseSetLogs.ToListAsync(token));
    }

    [Fact]
    public async Task Record_OpenEndedPlan_AcceptsDateAfterStart()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(token, endDate: null);
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var result = await new ExerciseSetLogStore(context).RecordAsync(
            seed.TrainerId,
            Model(seed.DayExerciseId, new DateTimeOffset(Now)),
            new DateTimeOffset(Now),
            Now,
            token);

        Assert.Equal(ExerciseSetLogStoreResult.Status.Recorded, result.Kind);
    }

    [Fact]
    public async Task Correct_ArchivedPlan_PreservesCreatedAtAndReference()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(token);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var store = new ExerciseSetLogStore(context);
        var recorded = await store.RecordAsync(
            seed.TrainerId,
            Model(seed.DayExerciseId, new DateTimeOffset(Now.AddMinutes(-5))),
            new DateTimeOffset(Now),
            Now,
            token);
        var original = recorded.Log!;
        var createdAt = original.CreatedAt;
        var reference = original.TrainingPlanDayExerciseId;
        context.ChangeTracker.Clear();
        var plan = await context.TrainingPlans.SingleAsync(
            candidate => candidate.Id == seed.PlanId, token);
        plan.Archive(Now.AddMinutes(1));
        await context.SaveChangesAsync(token);

        var result = await store.CorrectAsync(
            seed.TrainerId,
            new CorrectExerciseSetLogWriteModel(
                original.Id,
                45m,
                8,
                "Corrected",
                new DateTimeOffset(Now.AddMinutes(-4))),
            new DateTimeOffset(Now.AddMinutes(2)),
            Now.AddMinutes(2),
            token);

        Assert.Equal(ExerciseSetLogStoreResult.Status.Corrected, result.Kind);
        Assert.Equal(createdAt, result.Log!.CreatedAt);
        Assert.Equal(reference, result.Log.TrainingPlanDayExerciseId);
        Assert.Equal(45m, result.Log.WeightKg);
    }

    private async Task<Seed> SeedAsync(
        CancellationToken token,
        DateOnly? endDate = null)
    {
        var tenant = await _fixture.SeedTenantWithClientAsync(
            $"log-{Guid.NewGuid():N}", token);
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        var exercise = new Exercise(
            tenant.TrainerId,
            "Bench press",
            "Chest",
            null,
            null,
            null,
            null,
            Now);
        var plan = new TrainingPlan(
            tenant.TrainerId,
            tenant.ClientId,
            "Plan",
            null,
            null,
            null,
            new DateOnly(2026, 8, 1),
            endDate,
            Now);
        var prescribed = plan.AddDay(1, 1, null, Now)
            .AddExercise(exercise.Id, 1, null, null, null, Now);
        prescribed.AddSet(1, 10, 40m, 60, 90, Now);
        context.Exercises.Add(exercise);
        context.TrainingPlans.Add(plan);
        await context.SaveChangesAsync(token);
        return new Seed(tenant.TrainerId, plan.Id, prescribed.Id);
    }

    private static RecordExerciseSetLogWriteModel Model(
        Guid dayExerciseId,
        DateTimeOffset performedAt) => new(
            dayExerciseId,
            1,
            40m,
            10,
            null,
            performedAt);

    private sealed record Seed(Guid TrainerId, Guid PlanId, Guid DayExerciseId);
}
