using Application.Features.Training.ExerciseSetLogs.Abstractions;
using Application.Pagination;
using Domain.Entities.Training;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Training;

namespace Infrastructure.IntegrationTests.Training;

[Collection(PostgresCollection.Name)]
public sealed class TrainingQueriesTests
{
    private static readonly DateTime Now =
        new(2026, 8, 12, 12, 0, 0, DateTimeKind.Utc);
    private readonly PostgresContainerFixture _fixture;

    public TrainingQueriesTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ExerciseSetLogGet_ExistingLog_ReturnsProjectedDetails()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(token);
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var result = await new ExerciseSetLogQueries(context)
            .GetAsync(seed.LogId, token);

        Assert.NotNull(result);
        Assert.Equal(seed.PlanId, result.TrainingPlanId);
        Assert.Equal(seed.ExerciseId, result.ExerciseId);
        Assert.Equal("Bench press", result.ExerciseName);
    }

    [Fact]
    public async Task ExerciseSetLogGet_OtherTenant_ReturnsNotFound()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(token);
        var otherTenant = await _fixture.SeedTenantWithClientAsync(
            $"queries-other-{Guid.NewGuid():N}", token);
        await using var context = _fixture.CreateContext(otherTenant.TrainerId);

        var result = await new ExerciseSetLogQueries(context)
            .GetAsync(seed.LogId, token);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExerciseSetLogList_MultipleExecutions_ReturnsNewestFirst()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(token);
        await using var context = _fixture.CreateContext(seed.TrainerId);
        var newer = new ClientExerciseSetLog(
            seed.ClientId,
            seed.DayExerciseId,
            1,
            45m,
            8,
            null,
            new DateTimeOffset(Now.AddMinutes(-1)),
            Now);
        context.ClientExerciseSetLogs.Add(newer);
        await context.SaveChangesAsync(token);

        var result = await new ExerciseSetLogQueries(context).ListAsync(
            seed.ClientId,
            seed.PlanId,
            null,
            null,
            new PageRequest(1, 10),
            token);

        Assert.Equal([newer.Id, seed.LogId], result.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task TrainingPlanDetails_ExistingPlan_ReturnsOrderedOwnedTree()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await SeedAsync(token);
        await using var context = _fixture.CreateContext(seed.TrainerId);

        var result = await new TrainingPlanQueries(context)
            .GetDetailsAsync(seed.PlanId, token);

        Assert.NotNull(result);
        var day = Assert.Single(result.Days);
        var exercise = Assert.Single(day.Exercises);
        Assert.Equal(seed.ExerciseId, exercise.ExerciseId);
        Assert.Single(exercise.Sets);
    }

    private async Task<Seed> SeedAsync(CancellationToken token)
    {
        var tenant = await _fixture.SeedTenantWithClientAsync(
            $"queries-{Guid.NewGuid():N}", token);
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        var exercise = new Exercise(
            tenant.TrainerId,
            "Bench press",
            null,
            "Chest",
            "Barbell",
            null,
            null,
            Now);
        var plan = new TrainingPlan(
            tenant.TrainerId,
            tenant.ClientId,
            "Strength",
            null,
            null,
            null,
            new DateOnly(2026, 8, 1),
            null,
            Now);
        var dayExercise = plan.AddDay(1, 1, null, Now)
            .AddExercise(exercise.Id, 1, null, null, null, Now);
        dayExercise.AddSet(1, 10, 40m, 60, 90, Now);
        var log = new ClientExerciseSetLog(
            tenant.ClientId,
            dayExercise.Id,
            1,
            40m,
            10,
            null,
            new DateTimeOffset(Now.AddMinutes(-5)),
            Now);

        context.Exercises.Add(exercise);
        context.TrainingPlans.Add(plan);
        context.ClientExerciseSetLogs.Add(log);
        await context.SaveChangesAsync(token);

        return new Seed(
            tenant.TrainerId,
            tenant.ClientId,
            plan.Id,
            dayExercise.Id,
            exercise.Id,
            log.Id);
    }

    private sealed record Seed(
        Guid TrainerId,
        Guid ClientId,
        Guid PlanId,
        Guid DayExerciseId,
        Guid ExerciseId,
        Guid LogId);
}
