using Domain.Entities.Nutrition;
using Domain.Entities.Training;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Administration;

namespace Infrastructure.IntegrationTests.Administration;

[Collection(PostgresCollection.Name)]
public sealed class AdminOverviewQueriesTests : ReadQueriesIntegrationTestContext
{
    public AdminOverviewQueriesTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task AdminOverview_SeparatesGlobalAndPrivateCounts()
    {
        var token = TestContext.Current.CancellationToken;
        var seed = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);
        var marker = NewDiscriminator();
        await SeedPrivateFoodAsync(seed.TrainerId, $"overview-{marker}", token);
        await SeedGlobalFoodAsync($"overview-{marker}-global", token);

        await using var context = _fixture.CreateAdministrativeContext();
        var first = await new AdminOverviewQueries(context).GetAsync(token);

        await SeedPrivateFoodAsync(seed.TrainerId, $"overview-{marker}-2", token);

        await using var secondContext = _fixture.CreateAdministrativeContext();
        var second = await new AdminOverviewQueries(secondContext).GetAsync(token);

        Assert.Equal(first.PrivateFoods.TotalCount + 1, second.PrivateFoods.TotalCount);
        Assert.Equal(first.GlobalFoods.TotalCount, second.GlobalFoods.TotalCount);
        Assert.True(second.GlobalFoods.TotalCount >= 1);
    }

}
