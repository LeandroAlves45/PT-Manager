using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>Prova o contrato HTTP das tomas de suplementos do dia (Sprint 6A).</summary>
[Collection(ApiTestCollection.Name)]
public sealed class ClientPortalSupplementIntakesTests
{
    private readonly PostgresApiFixture _fixture;

    public ClientPortalSupplementIntakesTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MarkListAndUnmark_UpdatesTodaySummary()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var assignmentId = await PortalTestData.SeedSupplementAssignmentAsync(
            _fixture.Factory, trainerId, clientUserId, Token);
        var client = PortalClient(trainerId, clientUserId);
        var route = $"/api/v1/portal/my-supplements/{assignmentId}/intakes/today";

        var first = await client.PutAsync(route, content: null, Token);
        var repeated = await client.PutAsync(route, content: null, Token);
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (first.StatusCode, repeated.StatusCode));
        Assert.Equal(
            (await ReadJsonAsync(first)).GetProperty("taken_at").GetDateTime(),
            (await ReadJsonAsync(repeated)).GetProperty("taken_at").GetDateTime());

        var summary = await ReadJsonAsync(
            await client.GetAsync("/api/v1/portal/my-supplements/intakes/today", Token));
        Assert.True(summary.GetProperty("total_count").GetInt32() >= 1);
        Assert.Equal(1, summary.GetProperty("taken_count").GetInt32());

        var unmarked = await client.DeleteAsync(route, Token);
        var unmarkedAgain = await client.DeleteAsync(route, Token);
        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (unmarked.StatusCode, unmarkedAgain.StatusCode));

        var after = await ReadJsonAsync(
            await client.GetAsync("/api/v1/portal/my-supplements/intakes/today", Token));
        Assert.Equal(0, after.GetProperty("taken_count").GetInt32());
    }

    [Fact]
    public async Task MarkIntake_AssignmentOfAnotherClient_ReturnsNotFound()
    {
        var (trainerId, ownerUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var assignmentId = await PortalTestData.SeedSupplementAssignmentAsync(
            _fixture.Factory, trainerId, ownerUserId, Token);
        var intruderUserId = await PortalTestData.SeedSecondClientSameTrainerAsync(_fixture.Factory, trainerId, Token);

        var response = await PortalClient(trainerId, intruderUserId).PutAsync(
            $"/api/v1/portal/my-supplements/{assignmentId}/intakes/today", content: null, Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Intakes_WithTrainerToken_ReturnForbidden()
    {
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(Guid.NewGuid()));

        var list = await client.GetAsync("/api/v1/portal/my-supplements/intakes/today", Token);
        var mark = await client.PutAsync(
            $"/api/v1/portal/my-supplements/{Guid.NewGuid()}/intakes/today", content: null, Token);

        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (list.StatusCode, mark.StatusCode));
    }

    [Fact]
    public async Task ListTodayIntakes_StaysWithinQueryBudget()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var client = PortalClient(trainerId, clientUserId);

        using var scope = CommandCountingInterceptor.BeginScope();
        var response = await client.GetAsync("/api/v1/portal/my-supplements/intakes/today", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Fuso do trainer + cliente ativo + lista com estado da toma.
        Assert.True(scope.Count <= 3, $"Query budget exceeded: {scope.Count} commands. {string.Join(" | ", scope.Commands)}");
    }

    private HttpClient PortalClient(Guid trainerId, Guid clientUserId) =>
        _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId));

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ApiJsonPayload.Options, Token);
}
