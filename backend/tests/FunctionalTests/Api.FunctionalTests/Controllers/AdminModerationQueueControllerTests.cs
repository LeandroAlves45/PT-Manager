using System.Net;
using System.Text.Json;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

[Collection(ApiTestCollection.Name)]
public sealed class AdminModerationQueueControllerTests : ApiReadEndpointsTestContext
{
    public AdminModerationQueueControllerTests(PostgresApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ModerationQueue_WithSuperuser_ListsPrivateContentWithTrainerName()
    {
        var token = TestContext.Current.CancellationToken;
        var (trainerId, _) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, token);

        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueSuperuser(Guid.NewGuid()));

        var response = await client.GetAsync(
            "/api/v1/admin/content-moderation/foods?search=Portal%20rice&page_size=25", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var items = document.RootElement.GetProperty("items").EnumerateArray().ToList();

        Assert.NotEmpty(items);
        Assert.All(items, item =>
        {
            Assert.NotEqual(Guid.Empty, item.GetProperty("owner_trainer_id").GetGuid());
            // O email do trainer nunca entra no contrato.
            Assert.False(item.TryGetProperty("owner_trainer_email", out _));
        });
        var trainerItem = Assert.Single(
            items, item => item.GetProperty("owner_trainer_id").GetGuid() == trainerId);
        Assert.False(string.IsNullOrWhiteSpace(
            trainerItem.GetProperty("owner_trainer_name").GetString()));
    }

}
