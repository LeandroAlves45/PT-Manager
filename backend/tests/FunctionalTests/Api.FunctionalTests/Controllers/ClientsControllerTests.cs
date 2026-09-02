using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Prova os seis endpoints de Clients contra PostgreSQL real, incluindo isolamento
/// entre tenants e o envelope de paginação.
/// </summary>
/// <remarks>
/// Os testes atravessam o pipeline HTTP completo: validação bearer, estabelecimento
/// de tenant, autorização por policy e persistência. Nenhum substitui serviços do
/// host, porque o defeito da claim <c>trainer_id</c> mostrou que um duplo na fronteira
/// esconde precisamente as falhas que estes testes têm de apanhar.
/// </remarks>
[Collection(ApiTestCollection.Name)]
public sealed class ClientsControllerTests
{
    private readonly ApiWebApplicationFactory _factory;

    public ClientsControllerTests(PostgresApiFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _factory = fixture.Factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // ---------------------------------------------------------------- POST /clients

    [Fact]
    public async Task Create_WithTrainerToken_ReturnsCreatedWithLocationAndBody()
    {
        var trainer = await SeedTrainerAsync("create-happy");
        var client = TrainerClient(trainer.TrainerId);

        var response = await client.PostAsJsonAsync(
            "/api/v1/clients",
            ValidCreatePayload("Ana Ferreira"),
            Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await ReadJsonAsync(response);
        var id = body.GetProperty("id").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal("Ana Ferreira", body.GetProperty("name").GetString());
        Assert.True(body.GetProperty("is_active").GetBoolean());
        Assert.Equal($"/api/v1/clients/{id}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateOriginClient().PostAsJsonAsync(
            "/api/v1/clients",
            ValidCreatePayload("Sem Token"),
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithClientRole_ReturnsForbidden()
    {
        var trainer = await SeedTrainerAsync("create-forbidden");
        var client = _factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), trainer.TrainerId));

        var response = await client.PostAsJsonAsync(
            "/api/v1/clients",
            ValidCreatePayload("Role Errado"),
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithSuperuserRole_ReturnsForbidden()
    {
        var client = _factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueSuperuser(Guid.NewGuid()));

        var response = await client.PostAsJsonAsync(
            "/api/v1/clients",
            ValidCreatePayload("Superuser"),
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithInvalidBody_ReturnsBadRequestWithErrorsArray()
    {
        var trainer = await SeedTrainerAsync("create-invalid");
        var client = TrainerClient(trainer.TrainerId);

        var response = await client.PostAsJsonAsync(
            "/api/v1/clients",
            new
            {
                name = string.Empty,
                contact_email = "not-an-email",
                phone = string.Empty,
                birth_date = "1990-01-01",
                sex = "unknown"
            },
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        var body = await ReadJsonAsync(response);
        var errors = body.GetProperty("errors");
        Assert.Equal(JsonValueKind.Array, errors.ValueKind);
        Assert.NotEmpty(errors.EnumerateArray());

        var codes = errors.EnumerateArray()
            .Select(error => error.GetProperty("code").GetString())
            .ToArray();
        Assert.Contains("client_name_invalid", codes);
        Assert.Contains("client_sex_invalid", codes);
    }

    // ----------------------------------------------------------------- GET /clients

    [Fact]
    public async Task List_ReturnsOnlyTheClientsOfTheAuthenticatedTenant()
    {
        var mine = await SeedTrainerAsync("list-mine");
        var other = await SeedTrainerAsync("list-other");
        await SeedClientAsync(mine.TrainerId, "Cliente Proprio");
        await SeedClientAsync(other.TrainerId, "Cliente Alheio");

        var response = await TrainerClient(mine.TrainerId)
            .GetAsync("/api/v1/clients", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        var names = body.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .ToArray();

        Assert.Contains("Cliente Proprio", names);
        Assert.DoesNotContain("Cliente Alheio", names);
    }

    [Fact]
    public async Task List_ReturnsSnakeCaseEnvelopeWithFilterTotal()
    {
        var trainer = await SeedTrainerAsync("list-envelope");
        for (var index = 0; index < 3; index++)
            await SeedClientAsync(trainer.TrainerId, $"Envelope {index}");

        var response = await TrainerClient(trainer.TrainerId)
            .GetAsync("/api/v1/clients?page_number=1&page_size=2", Token);

        var body = await ReadJsonAsync(response);

        Assert.Equal(2, body.GetProperty("items").GetArrayLength());
        // total_count é o total do filtro, e não o tamanho da página devolvida.
        Assert.Equal(3, body.GetProperty("total_count").GetInt32());
        Assert.Equal(1, body.GetProperty("page_number").GetInt32());
        Assert.Equal(2, body.GetProperty("page_size").GetInt32());
    }

    [Fact]
    public async Task List_WithOmittedPagination_AppliesTheUseCaseDefaults()
    {
        var trainer = await SeedTrainerAsync("list-defaults");
        await SeedClientAsync(trainer.TrainerId, "Defaults");

        var response = await TrainerClient(trainer.TrainerId)
            .GetAsync("/api/v1/clients", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal(1, body.GetProperty("page_number").GetInt32());
        Assert.Equal(50, body.GetProperty("page_size").GetInt32());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 100)]
    public async Task List_WithAcceptedPaginationBoundaries_ReturnsOk(
        int pageNumber,
        int pageSize)
    {
        var trainer = await SeedTrainerAsync($"list-ok-{pageNumber}-{pageSize}");

        var response = await TrainerClient(trainer.TrainerId).GetAsync(
            $"/api/v1/clients?page_number={pageNumber}&page_size={pageSize}",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task List_WithPageSizeAboveTheLimit_ReturnsBadRequest()
    {
        var trainer = await SeedTrainerAsync("list-oversized");

        var response = await TrainerClient(trainer.TrainerId).GetAsync(
            "/api/v1/clients?page_number=1&page_size=101",
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await ReadJsonAsync(response);
        var codes = body.GetProperty("errors").EnumerateArray()
            .Select(error => error.GetProperty("code").GetString())
            .ToArray();
        Assert.Contains("page_size_invalid", codes);
    }

    [Fact]
    public async Task List_WithArchivedFilter_ExcludesActiveClients()
    {
        var trainer = await SeedTrainerAsync("list-archived");
        await SeedClientAsync(trainer.TrainerId, "Activo");
        await SeedClientAsync(trainer.TrainerId, "Arquivado", isActive: false);

        var response = await TrainerClient(trainer.TrainerId)
            .GetAsync("/api/v1/clients?activity=archived", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        var names = body.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .ToArray();

        Assert.Contains("Arquivado", names);
        Assert.DoesNotContain("Activo", names);
    }

    [Fact]
    public async Task List_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateOriginClient()
            .GetAsync("/api/v1/clients", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ------------------------------------------------------ GET /clients/{clientId}

    [Fact]
    public async Task Get_WithOwnClient_ReturnsDetails()
    {
        var trainer = await SeedTrainerAsync("get-happy");
        var clientId = await SeedClientAsync(trainer.TrainerId, "Detalhe");

        var response = await TrainerClient(trainer.TrainerId)
            .GetAsync($"/api/v1/clients/{clientId}", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal(clientId, body.GetProperty("id").GetGuid());
        Assert.Equal("Detalhe", body.GetProperty("name").GetString());
        Assert.Equal(
            JsonValueKind.Array,
            body.GetProperty("usable_packs").ValueKind);
    }

    [Fact]
    public async Task Get_WithClientOfAnotherTenant_ReturnsNotFoundAndNeverOk()
    {
        var mine = await SeedTrainerAsync("get-idor-mine");
        var other = await SeedTrainerAsync("get-idor-other");
        var foreignClientId = await SeedClientAsync(other.TrainerId, "Alheio");

        var response = await TrainerClient(mine.TrainerId)
            .GetAsync($"/api/v1/clients/{foreignClientId}", Token);

        // 404 e não 403: o tenant vizinho não deve sequer saber que o recurso existe.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithUnknownClient_ReturnsNotFound()
    {
        var trainer = await SeedTrainerAsync("get-unknown");

        var response = await TrainerClient(trainer.TrainerId)
            .GetAsync($"/api/v1/clients/{Guid.NewGuid()}", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateOriginClient()
            .GetAsync($"/api/v1/clients/{Guid.NewGuid()}", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithClientRole_ReturnsForbidden()
    {
        var trainer = await SeedTrainerAsync("get-forbidden");
        var clientId = await SeedClientAsync(trainer.TrainerId, "Alvo");
        var caller = _factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), trainer.TrainerId));

        var response = await caller.GetAsync($"/api/v1/clients/{clientId}", Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------------------------------------------------- PATCH /clients/{clientId}

    [Fact]
    public async Task Update_WithOwnClient_ReturnsUpdatedDetails()
    {
        var trainer = await SeedTrainerAsync("update-happy");
        var clientId = await SeedClientAsync(trainer.TrainerId, "Nome Antigo");

        var response = await TrainerClient(trainer.TrainerId).PatchAsJsonAsync(
            $"/api/v1/clients/{clientId}",
            ValidCreatePayload("Nome Novo"),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("Nome Novo", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Update_WithClientOfAnotherTenant_ReturnsNotFound()
    {
        var mine = await SeedTrainerAsync("update-idor-mine");
        var other = await SeedTrainerAsync("update-idor-other");
        var foreignClientId = await SeedClientAsync(other.TrainerId, "Alheio");

        var response = await TrainerClient(mine.TrainerId).PatchAsJsonAsync(
            $"/api/v1/clients/{foreignClientId}",
            ValidCreatePayload("Invasor"),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithInvalidBody_ReturnsBadRequestWithErrorsArray()
    {
        var trainer = await SeedTrainerAsync("update-invalid");
        var clientId = await SeedClientAsync(trainer.TrainerId, "Alvo");

        var response = await TrainerClient(trainer.TrainerId).PatchAsJsonAsync(
            $"/api/v1/clients/{clientId}",
            new
            {
                name = string.Empty,
                phone = string.Empty,
                birth_date = "1990-01-01",
                sex = "male"
            },
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.NotEmpty(body.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public async Task Update_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateOriginClient().PatchAsJsonAsync(
            $"/api/v1/clients/{Guid.NewGuid()}",
            ValidCreatePayload("Sem Token"),
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithClientRole_ReturnsForbidden()
    {
        var trainer = await SeedTrainerAsync("update-forbidden");
        var clientId = await SeedClientAsync(trainer.TrainerId, "Alvo");
        var caller = _factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), trainer.TrainerId));

        var response = await caller.PatchAsJsonAsync(
            $"/api/v1/clients/{clientId}",
            ValidCreatePayload("Role Errado"),
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -------------------------------------------- POST /clients/{clientId}/archive

    [Fact]
    public async Task Archive_WithOwnClient_ReturnsNoContentAndRemovesItFromActiveList()
    {
        var trainer = await SeedTrainerAsync("archive-happy");
        var clientId = await SeedClientAsync(trainer.TrainerId, "A Arquivar");
        var caller = TrainerClient(trainer.TrainerId);

        var response = await caller.PostAsync(
            $"/api/v1/clients/{clientId}/archive",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var listed = await ReadJsonAsync(
            await caller.GetAsync("/api/v1/clients", Token));
        var names = listed.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .ToArray();
        Assert.DoesNotContain("A Arquivar", names);
    }

    [Fact]
    public async Task Archive_WithClientOfAnotherTenant_ReturnsNotFound()
    {
        var mine = await SeedTrainerAsync("archive-idor-mine");
        var other = await SeedTrainerAsync("archive-idor-other");
        var foreignClientId = await SeedClientAsync(other.TrainerId, "Alheio");

        var response = await TrainerClient(mine.TrainerId).PostAsync(
            $"/api/v1/clients/{foreignClientId}/archive",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Archive_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateOriginClient().PostAsync(
            $"/api/v1/clients/{Guid.NewGuid()}/archive",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Archive_WithClientRole_ReturnsForbidden()
    {
        var trainer = await SeedTrainerAsync("archive-forbidden");
        var clientId = await SeedClientAsync(trainer.TrainerId, "Alvo");
        var caller = _factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), trainer.TrainerId));

        var response = await caller.PostAsync(
            $"/api/v1/clients/{clientId}/archive",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ----------------------------------------- POST /clients/{clientId}/reactivate

    [Fact]
    public async Task Reactivate_WithArchivedClient_ReturnsNoContentAndRestoresIt()
    {
        var trainer = await SeedTrainerAsync("reactivate-happy");
        var clientId = await SeedClientAsync(
            trainer.TrainerId,
            "A Reactivar",
            isActive: false);
        var caller = TrainerClient(trainer.TrainerId);

        var response = await caller.PostAsync(
            $"/api/v1/clients/{clientId}/reactivate",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var detail = await ReadJsonAsync(
            await caller.GetAsync($"/api/v1/clients/{clientId}", Token));
        Assert.True(detail.GetProperty("is_active").GetBoolean());
    }

    [Fact]
    public async Task Reactivate_WithClientOfAnotherTenant_ReturnsNotFound()
    {
        var mine = await SeedTrainerAsync("reactivate-idor-mine");
        var other = await SeedTrainerAsync("reactivate-idor-other");
        var foreignClientId = await SeedClientAsync(
            other.TrainerId,
            "Alheio",
            isActive: false);

        var response = await TrainerClient(mine.TrainerId).PostAsync(
            $"/api/v1/clients/{foreignClientId}/reactivate",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reactivate_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateOriginClient().PostAsync(
            $"/api/v1/clients/{Guid.NewGuid()}/reactivate",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reactivate_WithClientRole_ReturnsForbidden()
    {
        var trainer = await SeedTrainerAsync("reactivate-forbidden");
        var clientId = await SeedClientAsync(trainer.TrainerId, "Alvo");
        var caller = _factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), trainer.TrainerId));

        var response = await caller.PostAsync(
            $"/api/v1/clients/{clientId}/reactivate",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ------------------------------------------------------------------- QG4A-API-002

    [Fact]
    public async Task Responses_UseTheApiContractAndNeverLeakApplicationDtoShapes()
    {
        var trainer = await SeedTrainerAsync("contract-shape");
        var clientId = await SeedClientAsync(trainer.TrainerId, "Contrato");

        var response = await TrainerClient(trainer.TrainerId)
            .GetAsync($"/api/v1/clients/{clientId}", Token);

        var payload = await response.Content.ReadAsStringAsync(Token);

        // O DTO da Application seria serializado em PascalCase se fosse devolvido
        // directamente; a projecção obrigatória do contrato garante snake_case.
        Assert.Contains("\"contact_email\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"usable_packs\"", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("ContactEmail", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("UsablePacks", payload, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------- utilitários

    private Task<SeededTrainer> SeedTrainerAsync(string discriminator) =>
        TrainerTenantSeeder.SeedTrainerAsync(_factory, discriminator, Token);

    private Task<Guid> SeedClientAsync(Guid trainerId, string name, bool isActive = true) =>
        TrainerTenantSeeder.SeedClientAsync(_factory, trainerId, name, Token, isActive);

    private HttpClient TrainerClient(Guid trainerId) =>
        _factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueTrainer(trainerId));

    private static object ValidCreatePayload(string name) => new
    {
        name,
        contact_email = $"{Guid.NewGuid():N}@client.test",
        phone = $"+3519{Random.Shared.Next(10_000_000, 99_999_999)}",
        birth_date = "1990-05-20",
        sex = "female",
        objective = "Perder peso",
        notes = "Notas de teste",
        emergency_contact_name = "Contacto",
        emergency_contact_phone = "+351911111111"
    };

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync(Token);
        return JsonDocument.Parse(payload).RootElement.Clone();
    }
}
