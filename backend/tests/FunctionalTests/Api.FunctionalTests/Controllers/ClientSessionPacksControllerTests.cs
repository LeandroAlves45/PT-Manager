using System.Net;
using System.Text.Json;
using Api.Contracts.Packs;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Prova a atribuição de packs a clientes e o contrato distinto da rota
/// <c>usable</c>, que devolve um array simples.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class ClientSessionPacksControllerTests
{
    private readonly PostgresApiFixture _fixture;

    public ClientSessionPacksControllerTests(PostgresApiFixture fixture) =>
        _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Assign_WithTrainerToken_ReturnsCreatedWithFullBalance()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/client-session-packs",
            NewAssignment(tenant.ClientId, tenant.PackTypeId),
            Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await ReadJsonAsync(response);
        Assert.Equal(10, body.GetProperty("sessions_total").GetInt32());
        Assert.Equal(10, body.GetProperty("sessions_remaining").GetInt32());
    }

    [Fact]
    public async Task Assign_WithoutToken_ReturnsUnauthorized()
    {
        var client = _fixture.Factory.CreateOriginClient();

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/client-session-packs",
            NewAssignment(Guid.NewGuid(), Guid.NewGuid()),
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Assign_WithClientToken_ReturnsForbidden()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), tenant.TrainerId));

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/client-session-packs",
            NewAssignment(tenant.ClientId, tenant.PackTypeId),
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Assign_WithEmptyClientId_ReturnsValidationProblemDetails()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/client-session-packs",
            NewAssignment(Guid.Empty, tenant.PackTypeId),
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.NotEmpty(body.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public async Task Assign_WithClientFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(intruder.TrainerId);

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/client-session-packs",
            NewAssignment(owner.ClientId, intruder.PackTypeId),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithPackFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var packId = await TrainingTestData.SeedClientSessionPackAsync(
            _fixture.Factory, owner.TrainerId, owner.ClientId, owner.PackTypeId, Token);

        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(intruder.TrainerId);

        var response = await client.GetAsync(
            $"/api/v1/client-session-packs/{packId}",
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateExpectedEndDate_WithPackFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var packId = await TrainingTestData.SeedClientSessionPackAsync(
            _fixture.Factory, owner.TrainerId, owner.ClientId, owner.PackTypeId, Token);

        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(intruder.TrainerId);

        var response = await ApiJsonPayload.PatchAsync(
            client,
            $"/api/v1/client-session-packs/{packId}/expected-end-date",
            new UpdateClientSessionPackExpectedEndDateRequest(new DateOnly(2027, 1, 1)),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateExpectedEndDate_ChangesTheDate()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var packId = await TrainingTestData.SeedClientSessionPackAsync(
            _fixture.Factory, tenant.TrainerId, tenant.ClientId, tenant.PackTypeId, Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await ApiJsonPayload.PatchAsync(
            client,
            $"/api/v1/client-session-packs/{packId}/expected-end-date",
            new UpdateClientSessionPackExpectedEndDateRequest(new DateOnly(2027, 1, 1)),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("2027-01-01", body.GetProperty("expected_end_date").GetString());
    }

    [Fact]
    public async Task Cancel_WithPackFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var packId = await TrainingTestData.SeedClientSessionPackAsync(
            _fixture.Factory, owner.TrainerId, owner.ClientId, owner.PackTypeId, Token);

        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(intruder.TrainerId);

        var response = await client.PostAsync(
            $"/api/v1/client-session-packs/{packId}/cancel",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cancel_RemovesThePackFromTheUsableList()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var packId = await TrainingTestData.SeedClientSessionPackAsync(
            _fixture.Factory, tenant.TrainerId, tenant.ClientId, tenant.PackTypeId, Token);
        var client = TrainerClient(tenant.TrainerId);

        var cancel = await client.PostAsync(
            $"/api/v1/client-session-packs/{packId}/cancel",
            content: null,
            Token);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        var usable = await ReadJsonAsync(
            await client.GetAsync(
                $"/api/v1/client-session-packs/usable?client_id={tenant.ClientId}",
                Token));

        Assert.DoesNotContain(
            usable.EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == packId);
    }

    [Fact]
    public async Task ListUsable_ReturnsBareArrayWithoutPaginationEnvelope()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var packId = await TrainingTestData.SeedClientSessionPackAsync(
            _fixture.Factory, tenant.TrainerId, tenant.ClientId, tenant.PackTypeId, Token);
        var client = TrainerClient(tenant.TrainerId);

        var body = await ReadJsonAsync(
            await client.GetAsync(
                $"/api/v1/client-session-packs/usable?client_id={tenant.ClientId}",
                Token));

        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Contains(
            body.EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == packId);

        var raw = body.GetRawText();
        Assert.DoesNotContain("total_count", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("page_number", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("page_size", raw, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListUsable_WithClientFromAnotherTenant_ReturnsEmptyArray()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        await TrainingTestData.SeedClientSessionPackAsync(
            _fixture.Factory, owner.TrainerId, owner.ClientId, owner.PackTypeId, Token);

        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(intruder.TrainerId);

        var response = await client.GetAsync(
            $"/api/v1/client-session-packs/usable?client_id={owner.ClientId}",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Empty(body.EnumerateArray());
    }

    [Fact]
    public async Task ListUsable_WithClientToken_ReturnsForbidden()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), tenant.TrainerId));

        var response = await client.GetAsync(
            $"/api/v1/client-session-packs/usable?client_id={tenant.ClientId}",
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_ReturnsOnlyOwnPacks()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var ownPackId = await TrainingTestData.SeedClientSessionPackAsync(
            _fixture.Factory, owner.TrainerId, owner.ClientId, owner.PackTypeId, Token);

        var other = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var otherPackId = await TrainingTestData.SeedClientSessionPackAsync(
            _fixture.Factory, other.TrainerId, other.ClientId, other.PackTypeId, Token);

        var client = TrainerClient(owner.TrainerId);
        var body = await ReadJsonAsync(
            await client.GetAsync(
                "/api/v1/client-session-packs?page_number=1&page_size=100",
                Token));

        var ids = body.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid())
            .ToArray();

        Assert.Contains(ownPackId, ids);
        Assert.DoesNotContain(otherPackId, ids);
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
            $"/api/v1/client-session-packs?page_number=1&page_size={pageSize}",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task List_WithPageSizeAboveLimit_ReturnsBadRequest()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await client.GetAsync(
            "/api/v1/client-session-packs?page_number=1&page_size=101",
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_StaysWithinQueryBudget()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        for (var index = 0; index < 3; index++)
        {
            await TrainingTestData.SeedClientSessionPackAsync(
                _fixture.Factory,
                tenant.TrainerId,
                tenant.ClientId,
                tenant.PackTypeId,
                Token);
        }

        var client = TrainerClient(tenant.TrainerId);

        using var scope = CommandCountingInterceptor.BeginScope();
        var response = await client.GetAsync(
            "/api/v1/client-session-packs?page_number=1&page_size=50",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            scope.Count <= 2,
            $"Listing used {scope.Count} commands: {string.Join(" | ", scope.Commands)}");
    }

    private HttpClient TrainerClient(Guid trainerId) =>
        _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainerId));

    private static AssignClientSessionPackRequest NewAssignment(
        Guid clientId,
        Guid packTypeId) =>
        new(clientId, packTypeId, new DateOnly(2026, 9, 1), null);

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync(Token);
        return JsonDocument.Parse(payload).RootElement.Clone();
    }
}
