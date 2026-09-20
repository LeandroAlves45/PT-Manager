using System.Net;
using System.Text.Json;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

[Collection(ApiTestCollection.Name)]
public sealed class AdminOverviewControllerTests : ApiReadEndpointsTestContext
{
    public AdminOverviewControllerTests(PostgresApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task AdminOverview_WithSuperuser_ReturnsCounts()
    {
        var token = TestContext.Current.CancellationToken;
        await PortalTestData.SeedActiveClientAsync(_fixture.Factory, token);

        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueSuperuser(Guid.NewGuid()));

        var response = await client.GetAsync("/api/v1/admin/overview", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var root = document.RootElement;

        Assert.True(root.GetProperty("private_foods").GetProperty("total_count").GetInt32() >= 1);
        Assert.True(root.GetProperty("global_foods").GetProperty("total_count").GetInt32() >= 0);
        Assert.True(
            root.GetProperty("private_exercises").GetProperty("blocked_count").GetInt32() >= 0);
    }
}
