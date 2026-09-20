using Application.Features.Training.TrainingPlans.ListTrainingPlans;
using Application.Pagination;
using Domain.Entities.Training;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Training;

namespace Infrastructure.IntegrationTests.Training;

[Collection(PostgresCollection.Name)]
public sealed class TrainingPlanQueriesTests : ReadQueriesIntegrationTestContext
{
    public TrainingPlanQueriesTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task TrainingPlanQueries_EndsRangeFilter_ExcludesOpenEndedPlans()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);

        await using (var context = _fixture.CreateContext(seed.TrainerId))
        {
            context.TrainingPlans.Add(new TrainingPlan(
                seed.TrainerId, seed.ClientId, "Com fim", null, null, null,
                new DateOnly(2026, 8, 31), LocalToday.AddDays(3), Now));
            await context.SaveChangesAsync(token);
        }

        await using var readContext = _fixture.CreateContext(seed.TrainerId);

        var plans = await new TrainingPlanQueries(readContext).ListAsync(
            seed.ClientId,
            null,
            TrainingPlanActivityFilter.All,
            LocalToday,
            LocalToday.AddDays(7),
            new PageRequest(1, 50),
            token);
        Assert.Equal(1, plans.TotalCount);
    }

}
