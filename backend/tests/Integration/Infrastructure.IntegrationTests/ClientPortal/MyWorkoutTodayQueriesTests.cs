using Domain.Entities.Training;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.ClientPortal;

namespace Infrastructure.IntegrationTests.ClientPortal;

[Collection(PostgresCollection.Name)]
public sealed class MyWorkoutTodayQueriesTests : ReadQueriesIntegrationTestContext
{
    public MyWorkoutTodayQueriesTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task WorkoutTodayQueries_NeverReturnLogsOfAnotherClient()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);
        var otherTenant = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);

        Guid planId;
        Guid prescriptionId;
        await using (var context = _fixture.CreateContext(seed.TrainerId))
        {
            var exercise = IntegrationTestData.Exercise(seed.TrainerId, Now);
            var plan = new TrainingPlan(
                seed.TrainerId, seed.ClientId, "Força", null, null, null,
                new DateOnly(2026, 8, 31), null, Now);
            var day = plan.AddDay(3, 1, null, Now);
            var prescription = day.AddExercise(exercise.Id, 1, null, null, null, Now);
            prescription.AddSet(1, 8, 60m, 60, 90, Now, 8m);

            context.Exercises.Add(exercise);
            context.TrainingPlans.Add(plan);
            await context.SaveChangesAsync(token);

            context.ClientExerciseSetLogs.Add(new ClientExerciseSetLog(
                seed.ClientId,
                prescription.Id,
                1,
                62.5m,
                8,
                null,
                new DateTimeOffset(2026, 9, 3, 9, 0, 0, TimeSpan.Zero),
                Now,
                8m));
            context.WorkoutCompletions.Add(new WorkoutCompletion(
                seed.TrainerId, seed.ClientId, plan.Id, day.Id, LocalToday, null, Now));
            await context.SaveChangesAsync(token);

            planId = plan.Id;
            prescriptionId = prescription.Id;
            Assert.NotEqual(Guid.Empty, prescriptionId);
        }

        await using var readContext = _fixture.CreateContext(seed.TrainerId);
        var queries = new MyWorkoutTodayQueries(readContext);
        var from = new DateTimeOffset(2026, 9, 2, 23, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(1);

        var ownLogs = await queries.ListMyLogsAsync(
            seed.TrainerId, seed.ClientUserId, planId, from, to, token);
        var foreignLogs = await queries.ListMyLogsAsync(
            seed.TrainerId, otherTenant.ClientUserId, planId, from, to, token);

        Assert.Single(ownLogs);
        Assert.Empty(foreignLogs);
    }

}
