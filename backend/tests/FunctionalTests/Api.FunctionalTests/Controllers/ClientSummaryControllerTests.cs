using System.Net;
using System.Text.Json;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

[Collection(ApiTestCollection.Name)]
public sealed class ClientSummaryControllerTests : ApiReadEndpointsTestContext
{
    public ClientSummaryControllerTests(PostgresApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task ClientSummary_WithSeededClient_ReturnsProgress()
    {
        var token = TestContext.Current.CancellationToken;
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, token);
        var clientId = await PortalTestData.GetClientIdAsync(_fixture.Factory, trainerId, clientUserId, token);

        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainerId));

        var response = await client.GetAsync($"/api/v1/clients/{clientId}/summary", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var root = document.RootElement;

        Assert.Equal(clientId, root.GetProperty("client_id").GetGuid());
        // A avaliação inicial dá peso e altura mesmo sem check-ins respondidos.
        Assert.Equal("initial_assessment", root.GetProperty("weight").GetProperty("source").GetString());
        Assert.True(root.GetProperty("height_cm").GetInt32() > 0);
        Assert.Equal("Portal plan", root.GetProperty("training_plan").GetProperty("name").GetString());
        Assert.True(root.TryGetProperty("adherence", out var adherence));
        Assert.True(adherence.GetProperty("planned_sets").GetInt32() >= 0);
        Assert.Equal(0, root.GetProperty("packs").GetProperty("usable_pack_count").GetInt32());
    }

    [Fact]
    public async Task ClientSummary_WithUnknownClient_ReturnsNotFound()
    {
        var token = TestContext.Current.CancellationToken;
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(
            _fixture.Factory, $"sum-{Guid.NewGuid():N}", token);

        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainer.TrainerId));

        var response = await client.GetAsync($"/api/v1/clients/{Guid.NewGuid()}/summary", token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ClientSummary_FromAnotherTrainer_ReturnsNotFound()
    {
        var token = TestContext.Current.CancellationToken;
        var (ownTrainerId, _) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, token);
        var (foreignTrainerId, foreignClientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, token);
        var foreignClientId = await PortalTestData.GetClientIdAsync(
            _fixture.Factory, foreignTrainerId, foreignClientUserId, token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(ownTrainerId));

        var response = await client.GetAsync($"/api/v1/clients/{foreignClientId}/summary", token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
