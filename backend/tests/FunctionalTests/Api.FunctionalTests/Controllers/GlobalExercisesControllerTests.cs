using System.Net;
using System.Text.Json;
using Api.Authorization;
using Api.Contracts.Training;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Prova que a curadoria do catálogo global exige superuser em contexto
/// administrativo e recusa qualquer outro ator.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class GlobalExercisesControllerTests
{
    private readonly PostgresApiFixture _fixture;

    public GlobalExercisesControllerTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_WithSuperuserToken_ReturnsCreatedWithLocation()
    {
        var client = await SuperuserClientAsync();

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/global-exercises",
            NewGlobalExercise($"Peso morto {Guid.NewGuid():N}"),
            Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await ReadJsonAsync(response);
        Assert.True(body.GetProperty("is_active").GetBoolean());
    }

    [Theory]
    [InlineData("POST", "/api/v1/global-exercises")]
    [InlineData("GET", "/api/v1/global-exercises")]
    public async Task CollectionRoutes_WithTrainerToken_ReturnForbidden(
        string method,
        string route)
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(tenant.TrainerId));

        var response = await SendAsync(client, method, route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("PATCH", "")]
    [InlineData("DELETE", "")]
    [InlineData("POST", "/archive")]
    [InlineData("POST", "/reactivate")]
    public async Task ItemRoutes_WithTrainerToken_ReturnForbidden(
        string method,
        string suffix)
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var exerciseId = await TrainingTestData.SeedGlobalExerciseAsync(
            _fixture.Factory, $"Alvo {Guid.NewGuid():N}", Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(tenant.TrainerId));

        var response = await SendAsync(
            client,
            method,
            $"/api/v1/global-exercises/{exerciseId}{suffix}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithClientToken_ReturnsForbidden()
    {
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), Guid.NewGuid()));

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/global-exercises",
            NewGlobalExercise("Cliente não cura catálogo"),
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        var client = _fixture.Factory.CreateOriginClient();

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/global-exercises",
            NewGlobalExercise("Sem token"),
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithUnknownIdentifier_ReturnsNotFound()
    {
        var client = await SuperuserClientAsync();

        var response = await client.GetAsync(
            $"/api/v1/global-exercises/{Guid.NewGuid()}",
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithEmptyName_ReturnsValidationProblemDetails()
    {
        var client = await SuperuserClientAsync();

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/global-exercises",
            NewGlobalExercise("  "),
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.NotEmpty(body.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public async Task ArchiveThenReactivate_TogglesCatalogAvailability()
    {
        var client = await SuperuserClientAsync();
        var exerciseId = await TrainingTestData.SeedGlobalExerciseAsync(
            _fixture.Factory, $"Remada {Guid.NewGuid():N}", Token);

        var archive = await client.PostAsync(
            $"/api/v1/global-exercises/{exerciseId}/archive",
            content: null,
            Token);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var archived = await ReadJsonAsync(
            await client.GetAsync($"/api/v1/global-exercises/{exerciseId}", Token));
        Assert.False(archived.GetProperty("is_active").GetBoolean());

        var reactivate = await client.PostAsync(
            $"/api/v1/global-exercises/{exerciseId}/reactivate",
            content: null,
            Token);
        Assert.Equal(HttpStatusCode.NoContent, reactivate.StatusCode);

        var reactivated = await ReadJsonAsync(
            await client.GetAsync($"/api/v1/global-exercises/{exerciseId}", Token));
        Assert.True(reactivated.GetProperty("is_active").GetBoolean());
    }

    [Fact]
    public async Task Delete_WithoutReferences_RemovesTheExercise()
    {
        var client = await SuperuserClientAsync();
        var exerciseId = await TrainingTestData.SeedGlobalExerciseAsync(
            _fixture.Factory, $"Descartável {Guid.NewGuid():N}", Token);

        var deleted = await client.DeleteAsync(
            $"/api/v1/global-exercises/{exerciseId}",
            Token);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var afterDelete = await client.GetAsync(
            $"/api/v1/global-exercises/{exerciseId}",
            Token);
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task List_StaysWithinQueryBudget()
    {
        var client = await SuperuserClientAsync();
        for (var index = 0; index < 5; index++)
        {
            await TrainingTestData.SeedGlobalExerciseAsync(
                _fixture.Factory, $"Global orçamento {index} {Guid.NewGuid():N}", Token);
        }

        using var scope = CommandCountingInterceptor.BeginScope();
        var response = await client.GetAsync(
            "/api/v1/global-exercises?page_number=1&page_size=50",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            scope.Count <= 2,
            $"Listagem custou {scope.Count} comandos: {string.Join(" | ", scope.Commands)}");
    }

    /// <summary>
    /// Prova a exigência de contexto administrativo sem depender de nenhuma rota:
    /// a policy é resolvida contra um endpoint sem o atributo.
    /// </summary>
    [Fact]
    public async Task SuperuserToken_OnRouteWithoutAdministrativeContext_IsNotAdministrative()
    {
        var superuserId = await TrainingTestData.SeedSuperuserAsync(_fixture.Factory, Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueSuperuser(superuserId));

        // /api/v1/clients é uma rota de personal trainer, sem [AdministrativeContext].
        var response = await client.GetAsync("/api/v1/clients", Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void GlobalExercisesController_DeclaresAdministrativeContext()
    {
        var controller = typeof(Api.Controllers.GlobalExercisesController);

        Assert.NotNull(
            controller.GetCustomAttributes(typeof(AdministrativeContextAttribute), false)
                .SingleOrDefault());
    }

    private async Task<HttpClient> SuperuserClientAsync()
    {
        var superuserId = await TrainingTestData.SeedSuperuserAsync(_fixture.Factory, Token);
        return _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueSuperuser(superuserId));
    }

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        string method,
        string route) => method switch
        {
            "GET" => client.GetAsync(route, Token),
            "DELETE" => client.DeleteAsync(route, Token),
            "POST" => ApiJsonPayload.PostAsync(
                client, route, NewGlobalExercise("Proibido"), Token),
            "PATCH" => ApiJsonPayload.PatchAsync(
                client,
                route,
                new UpdateGlobalExerciseRequest("Proibido", null, null, null, null, null),
                Token),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
        };

    private static CreateGlobalExerciseRequest NewGlobalExercise(string name) =>
        new(name, null, "costas", "barra", "advanced", null);

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync(Token);
        return JsonDocument.Parse(payload).RootElement.Clone();
    }
}
