using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Clients;

namespace Infrastructure.IntegrationTests.Clients;

[Collection(PostgresCollection.Name)]
public sealed class ClientProgressSummaryQueriesTests : ReadQueriesIntegrationTestContext
{
    public ClientProgressSummaryQueriesTests(PostgresContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetAsync_WithClientFromAnotherTrainer_ReturnsNull()
    {
        var token = TestContext.Current.CancellationToken;
        var tenant = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);
        var otherTenant = await _fixture.SeedTenantWithClientAsync(NewDiscriminator(), token);
        await using var context = _fixture.CreateContext(tenant.TrainerId);
        var queries = new ClientProgressSummaryQueries(context);

        var result = await queries.GetAsync(
            otherTenant.ClientId,
            LocalToday.AddDays(-56),
            new DateTimeOffset(Now.AddDays(-28), TimeSpan.Zero),
            new DateTimeOffset(Now.AddDays(1), TimeSpan.Zero),
            token);

        Assert.Null(result);
    }
}
