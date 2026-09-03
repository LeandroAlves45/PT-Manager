using System.Net;
using System.Text.Json;
using Api.Contracts.Sessions;
using Api.FunctionalTests.Support;
using Domain.ValueObjects;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Prova a agenda de sessões: contrato HTTP, máquina de estados e os erros próprios
/// do feature Sessions.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class SessionsControllerTests
{
    private readonly PostgresApiFixture _fixture;

    public SessionsControllerTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_WithTrainerToken_ReturnsCreatedScheduled()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/sessions",
            NewSession(tenant.ClientId, DateTimeOffset.UtcNow.AddDays(3)),
            Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await ReadJsonAsync(response);
        Assert.Equal("scheduled", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        var client = _fixture.Factory.CreateOriginClient();

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/sessions",
            NewSession(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1)),
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
            "/api/v1/sessions",
            NewSession(tenant.ClientId, DateTimeOffset.UtcNow.AddDays(2)),
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithPastStart_ReturnsValidationProblemDetails()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/sessions",
            NewSession(tenant.ClientId, DateTimeOffset.UtcNow.AddDays(-1)),
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithClientFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(intruder.TrainerId);

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/sessions",
            NewSession(owner.ClientId, DateTimeOffset.UtcNow.AddDays(4)),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithSessionFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var sessionId = await TrainingTestData.SeedSessionAsync(
            _fixture.Factory,
            owner.TrainerId,
            owner.ClientId,
            null,
            DateTimeOffset.UtcNow.AddDays(5),
            Token);

        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(intruder.TrainerId);

        var response = await client.GetAsync($"/api/v1/sessions/{sessionId}", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reschedule_MovesTheSessionAndKeepsItScheduled()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var sessionId = await TrainingTestData.SeedSessionAsync(
            _fixture.Factory,
            tenant.TrainerId,
            tenant.ClientId,
            null,
            DateTimeOffset.UtcNow.AddDays(6),
            Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await ApiJsonPayload.PatchAsync(
            client,
            $"/api/v1/sessions/{sessionId}/reschedule",
            new RescheduleSessionRequest(
                DateTimeOffset.UtcNow.AddDays(9),
                45,
                "Ginásio"),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("scheduled", body.GetProperty("status").GetString());
        Assert.Equal(45, body.GetProperty("duration_minutes").GetInt32());
    }

    [Theory]
    [InlineData("complete", "completed")]
    [InlineData("cancel-by-trainer", "cancelled_by_trainer")]
    [InlineData("cancel-by-client", "cancelled_by_client")]
    [InlineData("no-show", "no_show")]
    public async Task Transition_FromScheduled_ReturnsOkWithNewStatus(
        string route,
        string expectedStatus)
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        // Complete e no-show exigem que a sessão já tenha começado.
        var sessionId = await TrainingTestData.SeedSessionAsync(
            _fixture.Factory,
            tenant.TrainerId,
            tenant.ClientId,
            null,
            DateTimeOffset.UtcNow.AddHours(-2),
            Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await client.PostAsync(
            $"/api/v1/sessions/{sessionId}/{route}",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal(expectedStatus, body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Restore_FromCancelled_ReturnsScheduled()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var sessionId = await TrainingTestData.SeedSessionAsync(
            _fixture.Factory,
            tenant.TrainerId,
            tenant.ClientId,
            null,
            DateTimeOffset.UtcNow.AddDays(7),
            Token,
            SessionStatus.CancelledByTrainer);
        var client = TrainerClient(tenant.TrainerId);

        var response = await client.PostAsync(
            $"/api/v1/sessions/{sessionId}/restore",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("scheduled", body.GetProperty("status").GetString());
    }

    /// <summary>
    /// A transição para o estado em que a sessão já está é idempotente: 200, não 409.
    /// </summary>
    [Fact]
    public async Task Transition_ToCurrentStatus_IsIdempotent()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var sessionId = await TrainingTestData.SeedSessionAsync(
            _fixture.Factory,
            tenant.TrainerId,
            tenant.ClientId,
            null,
            DateTimeOffset.UtcNow.AddHours(-3),
            Token,
            SessionStatus.Completed);
        var client = TrainerClient(tenant.TrainerId);

        var response = await client.PostAsync(
            $"/api/v1/sessions/{sessionId}/complete",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("completed", body.GetProperty("status").GetString());
    }

    /// <summary>
    /// Fora de <c>restore</c>, sair de um estado final é inválido e devolve 409
    /// <c>session_invalid_state</c>.
    /// </summary>
    [Theory]
    [InlineData("cancel-by-trainer")]
    [InlineData("cancel-by-client")]
    [InlineData("no-show")]
    public async Task Transition_FromTerminalStatus_ReturnsConflict(string route)
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var sessionId = await TrainingTestData.SeedSessionAsync(
            _fixture.Factory,
            tenant.TrainerId,
            tenant.ClientId,
            null,
            DateTimeOffset.UtcNow.AddHours(-4),
            Token,
            SessionStatus.Completed);
        var client = TrainerClient(tenant.TrainerId);

        var response = await client.PostAsync(
            $"/api/v1/sessions/{sessionId}/{route}",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("session_invalid_state", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Complete_BeforeTheSessionStarts_ReturnsConflict()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var sessionId = await TrainingTestData.SeedSessionAsync(
            _fixture.Factory,
            tenant.TrainerId,
            tenant.ClientId,
            null,
            DateTimeOffset.UtcNow.AddDays(8),
            Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await client.PostAsync(
            $"/api/v1/sessions/{sessionId}/complete",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("session_transition_too_early", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task CancelByClient_WithClientToken_ReturnsForbidden()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var sessionId = await TrainingTestData.SeedSessionAsync(
            _fixture.Factory,
            tenant.TrainerId,
            tenant.ClientId,
            null,
            DateTimeOffset.UtcNow.AddDays(10),
            Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), tenant.TrainerId));

        var response = await client.PostAsync(
            $"/api/v1/sessions/{sessionId}/cancel-by-client",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("cancel-by-trainer")]
    [InlineData("cancel-by-client")]
    [InlineData("no-show")]
    [InlineData("restore")]
    public async Task Transition_WithSessionFromAnotherTenant_ReturnsNotFound(string route)
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var sessionId = await TrainingTestData.SeedSessionAsync(
            _fixture.Factory,
            owner.TrainerId,
            owner.ClientId,
            null,
            DateTimeOffset.UtcNow.AddHours(-5),
            Token);

        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(intruder.TrainerId);

        var response = await client.PostAsync(
            $"/api/v1/sessions/{sessionId}/{route}",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Complete_WithPack_ConsumesOneSessionFromTheBalance()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var packId = await TrainingTestData.SeedClientSessionPackAsync(
            _fixture.Factory,
            tenant.TrainerId,
            tenant.ClientId,
            tenant.PackTypeId,
            Token);
        var sessionId = await TrainingTestData.SeedSessionAsync(
            _fixture.Factory,
            tenant.TrainerId,
            tenant.ClientId,
            packId,
            DateTimeOffset.UtcNow.AddHours(-6),
            Token);
        var client = TrainerClient(tenant.TrainerId);

        var before = await TrainingTestData.ReadPackBalanceAsync(
            _fixture.Factory, tenant.TrainerId, packId, Token);

        var response = await client.PostAsync(
            $"/api/v1/sessions/{sessionId}/complete",
            content: null,
            Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = await TrainingTestData.ReadPackBalanceAsync(
            _fixture.Factory, tenant.TrainerId, packId, Token);

        Assert.Equal(before - 1, after);
    }

    [Fact]
    public async Task ChangePack_WithPackFromAnotherClient_ReturnsNotFound()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var otherClientId = await TrainerTenantSeeder.SeedClientAsync(
            _fixture.Factory, tenant.TrainerId, "Outro cliente do tenant", Token);
        var otherPackId = await TrainingTestData.SeedClientSessionPackAsync(
            _fixture.Factory,
            tenant.TrainerId,
            otherClientId,
            tenant.PackTypeId,
            Token);
        var sessionId = await TrainingTestData.SeedSessionAsync(
            _fixture.Factory,
            tenant.TrainerId,
            tenant.ClientId,
            null,
            DateTimeOffset.UtcNow.AddDays(11),
            Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await ApiJsonPayload.PatchAsync(
            client,
            $"/api/v1/sessions/{sessionId}/pack",
            new ChangeSessionPackRequest(otherPackId),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("session_pack_not_available", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task ChangePack_WithOwnPack_AssociatesTheSession()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var packId = await TrainingTestData.SeedClientSessionPackAsync(
            _fixture.Factory,
            tenant.TrainerId,
            tenant.ClientId,
            tenant.PackTypeId,
            Token);
        var sessionId = await TrainingTestData.SeedSessionAsync(
            _fixture.Factory,
            tenant.TrainerId,
            tenant.ClientId,
            null,
            DateTimeOffset.UtcNow.AddDays(12),
            Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await ApiJsonPayload.PatchAsync(
            client,
            $"/api/v1/sessions/{sessionId}/pack",
            new ChangeSessionPackRequest(packId),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal(packId, body.GetProperty("client_session_pack_id").GetGuid());
    }

    [Fact]
    public async Task List_ReturnsOnlyOwnSessions()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var other = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        await TrainingTestData.SeedSessionAsync(
            _fixture.Factory, owner.TrainerId, owner.ClientId, null,
            DateTimeOffset.UtcNow.AddDays(13), Token);
        await TrainingTestData.SeedSessionAsync(
            _fixture.Factory, other.TrainerId, other.ClientId, null,
            DateTimeOffset.UtcNow.AddDays(13), Token);

        var client = TrainerClient(owner.TrainerId);
        var body = await ReadJsonAsync(
            await client.GetAsync("/api/v1/sessions?page_number=1&page_size=50", Token));

        var clientIds = body.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("client_id").GetGuid())
            .ToArray();

        Assert.All(clientIds, id => Assert.Equal(owner.ClientId, id));
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
            $"/api/v1/sessions?page_number=1&page_size={pageSize}",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task List_WithPageSizeAboveLimit_ReturnsBadRequest()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await client.GetAsync(
            "/api/v1/sessions?page_number=1&page_size=101",
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_StaysWithinQueryBudget()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        for (var index = 1; index <= 5; index++)
        {
            await TrainingTestData.SeedSessionAsync(
                _fixture.Factory,
                tenant.TrainerId,
                tenant.ClientId,
                null,
                DateTimeOffset.UtcNow.AddDays(20 + index),
                Token);
        }

        var client = TrainerClient(tenant.TrainerId);

        using var scope = CommandCountingInterceptor.BeginScope();
        var response = await client.GetAsync(
            "/api/v1/sessions?page_number=1&page_size=50",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            scope.Count <= 2,
            $"Listagem custou {scope.Count} comandos: {string.Join(" | ", scope.Commands)}");
    }

    private HttpClient TrainerClient(Guid trainerId) =>
        _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainerId));

    private static CreateSessionRequest NewSession(Guid clientId, DateTimeOffset startsAt) =>
        new(clientId, null, startsAt, 60, "Estúdio", "personal", null);

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync(Token);
        return JsonDocument.Parse(payload).RootElement.Clone();
    }
}
