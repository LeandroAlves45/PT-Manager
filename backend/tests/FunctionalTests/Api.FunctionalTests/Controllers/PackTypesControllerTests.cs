using System.Net;
using System.Text.Json;
using Api.Contracts.Packs;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>Prova o catálogo de tipos de pack do personal trainer.</summary>
[Collection(ApiTestCollection.Name)]
public sealed class PackTypesControllerTests
{
    private readonly PostgresApiFixture _fixture;

    public PackTypesControllerTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_WithTrainerToken_ReturnsCreatedWithLocation()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/pack-types",
            new CreatePackTypeRequest("Pack 5", 5, 12_500, "EUR", 60),
            Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await ReadJsonAsync(response);
        Assert.Equal(5, body.GetProperty("session_count").GetInt32());
        Assert.Equal(12_500, body.GetProperty("price_cents").GetInt32());
        Assert.True(body.GetProperty("is_active").GetBoolean());
    }

    [Fact]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        var client = _fixture.Factory.CreateOriginClient();

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/pack-types",
            new CreatePackTypeRequest("No token", 5, 100, "EUR", null),
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithClientToken_ReturnsForbidden()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), tenant.TrainerId));

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/pack-types",
            new CreatePackTypeRequest("Client does not sell packs", 5, 100, "EUR", null),
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithZeroSessions_ReturnsValidationProblemDetails()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/pack-types",
            new CreatePackTypeRequest("Invalid", 0, 100, "EUR", null),
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.NotEmpty(body.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public async Task Get_WithPackTypeFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(intruder.TrainerId);

        var response = await client.GetAsync(
            $"/api/v1/pack-types/{owner.PackTypeId}",
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithPackTypeFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(intruder.TrainerId);

        var response = await ApiJsonPayload.PatchAsync(
            client,
            $"/api/v1/pack-types/{owner.PackTypeId}",
            new UpdatePackTypeRequest("Stolen", 5, 100, "EUR", null),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ArchiveAndReactivate_WithPackTypeFromAnotherTenant_ReturnNotFound()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(intruder.TrainerId);

        var archive = await client.PostAsync(
            $"/api/v1/pack-types/{owner.PackTypeId}/archive", content: null, Token);
        var reactivate = await client.PostAsync(
            $"/api/v1/pack-types/{owner.PackTypeId}/reactivate", content: null, Token);

        Assert.Equal(HttpStatusCode.NotFound, archive.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, reactivate.StatusCode);
    }

    [Fact]
    public async Task Archive_PreservesAssignedPacksBalance()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var packId = await TrainingTestData.SeedClientSessionPackAsync(
            _fixture.Factory,
            tenant.TrainerId,
            tenant.ClientId,
            tenant.PackTypeId,
            Token);
        var client = TrainerClient(tenant.TrainerId);

        var before = await TrainingTestData.ReadPackBalanceAsync(
            _fixture.Factory, tenant.TrainerId, packId, Token);

        var archive = await client.PostAsync(
            $"/api/v1/pack-types/{tenant.PackTypeId}/archive", content: null, Token);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var after = await TrainingTestData.ReadPackBalanceAsync(
            _fixture.Factory, tenant.TrainerId, packId, Token);
        Assert.Equal(before, after);

        // O pack atribuído continua visível e utilizável.
        var usable = await ReadJsonAsync(
            await client.GetAsync(
                $"/api/v1/client-session-packs/usable?client_id={tenant.ClientId}",
                Token));
        Assert.Contains(
            usable.EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == packId);
    }

    [Fact]
    public async Task Archive_PreventsNewAssignments()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var archive = await client.PostAsync(
            $"/api/v1/pack-types/{tenant.PackTypeId}/archive", content: null, Token);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/client-session-packs",
            new AssignClientSessionPackRequest(
                tenant.ClientId,
                tenant.PackTypeId,
                new DateOnly(2026, 9, 1),
                null),
            Token);

        Assert.True(
            response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound,
            $"Assignment on archived type returned {(int)response.StatusCode}.");
    }

    [Fact]
    public async Task List_ReturnsOnlyOwnPackTypes()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var other = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        await TrainingTestData.SeedPackTypeAsync(
            _fixture.Factory, other.TrainerId, "Pack from other tenant", 8, Token);

        var client = TrainerClient(owner.TrainerId);
        var body = await ReadJsonAsync(
            await client.GetAsync("/api/v1/pack-types?page_number=1&page_size=100", Token));

        var names = body.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .ToArray();

        Assert.Contains("Pack 10 sessions", names);
        Assert.DoesNotContain("Pack from other tenant", names);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    public async Task List_WithAcceptedPageSizes_ReturnsOk(int pageSize)
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await client.GetAsync(
            $"/api/v1/pack-types?page_number=1&page_size={pageSize}",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task List_WithPageSizeAboveLimit_ReturnsBadRequest()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await client.GetAsync(
            "/api/v1/pack-types?page_number=1&page_size=101",
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_StaysWithinQueryBudget()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        for (var index = 0; index < 5; index++)
        {
            await TrainingTestData.SeedPackTypeAsync(
                _fixture.Factory, tenant.TrainerId, $"Pack {index}", 5 + index, Token);
        }

        var client = TrainerClient(tenant.TrainerId);

        using var scope = CommandCountingInterceptor.BeginScope();
        var response = await client.GetAsync(
            "/api/v1/pack-types?page_number=1&page_size=50",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            scope.Count <= 2,
            $"Listing used {scope.Count} commands: {string.Join(" | ", scope.Commands)}");
    }

    private HttpClient TrainerClient(Guid trainerId) =>
        _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainerId));

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync(Token);
        return JsonDocument.Parse(payload).RootElement.Clone();
    }
}
