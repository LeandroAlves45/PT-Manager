using System.Net;
using System.Text.Json;
using Api.Contracts.Training;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>Prova o contrato HTTP, o isolamento por tenant e as policies de Exercises.</summary>
[Collection(ApiTestCollection.Name)]
public sealed class ExercisesControllerTests
{
    private readonly PostgresApiFixture _fixture;

    public ExercisesControllerTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_WithTrainerToken_ReturnsCreatedWithLocation()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/exercises",
            NewExercise("Flat bench press"),
            Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await ReadJsonAsync(response);
        Assert.Equal("Flat bench press", body.GetProperty("name").GetString());
        Assert.True(body.GetProperty("is_active").GetBoolean());
    }

    [Fact]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        var client = _fixture.Factory.CreateOriginClient();

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/exercises",
            NewExercise("No token"),
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(
            response.Headers.WwwAuthenticate,
            header => header.Scheme == "Bearer");
    }

    [Fact]
    public async Task Create_WithClientToken_ReturnsForbidden()
    {
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), Guid.NewGuid()));

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/exercises",
            NewExercise("Client cannot prescribe"),
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithEmptyName_ReturnsValidationProblemDetails()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/exercises",
            NewExercise("   "),
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.NotEmpty(body.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public async Task Get_WithExerciseFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(intruder.TrainerId);

        var response = await client.GetAsync(
            $"/api/v1/exercises/{owner.ExerciseId}",
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithExerciseFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(intruder.TrainerId);

        var response = await ApiJsonPayload.PatchAsync(
            client,
            $"/api/v1/exercises/{owner.ExerciseId}",
            new UpdateExerciseRequest("Renamed", null, null, null, null, null),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ArchiveAndReactivate_WithExerciseFromAnotherTenant_ReturnNotFound()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(intruder.TrainerId);

        var archive = await client.PostAsync(
            $"/api/v1/exercises/{owner.ExerciseId}/archive",
            content: null,
            Token);
        var reactivate = await client.PostAsync(
            $"/api/v1/exercises/{owner.ExerciseId}/reactivate",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.NotFound, archive.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, reactivate.StatusCode);
    }

    [Fact]
    public async Task ArchiveThenReactivate_RestoresTheExercise()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var archive = await client.PostAsync(
            $"/api/v1/exercises/{tenant.ExerciseId}/archive",
            content: null,
            Token);
        Assert.Equal(HttpStatusCode.NoContent, archive.StatusCode);

        var archived = await ReadJsonAsync(
            await client.GetAsync($"/api/v1/exercises/{tenant.ExerciseId}", Token));
        Assert.False(archived.GetProperty("is_active").GetBoolean());

        var reactivate = await client.PostAsync(
            $"/api/v1/exercises/{tenant.ExerciseId}/reactivate",
            content: null,
            Token);
        Assert.Equal(HttpStatusCode.NoContent, reactivate.StatusCode);

        var reactivated = await ReadJsonAsync(
            await client.GetAsync($"/api/v1/exercises/{tenant.ExerciseId}", Token));
        Assert.True(reactivated.GetProperty("is_active").GetBoolean());
    }

    [Fact]
    public async Task List_ReturnsGlobalCatalogAndOwnPrivateExercisesOnly()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var other = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);

        var globalName = $"Global {Guid.NewGuid():N}";
        await TrainingTestData.SeedGlobalExerciseAsync(_fixture.Factory, globalName, Token);
        await TrainingTestData.SeedPrivateExerciseAsync(
            _fixture.Factory, other.TrainerId, "Other tenant private", Token);

        var client = TrainerClient(owner.TrainerId);
        var ownExercise = await ReadJsonAsync(
            await client.GetAsync($"/api/v1/exercises/{owner.ExerciseId}", Token));
        var body = await ReadJsonAsync(
            await client.GetAsync("/api/v1/exercises?page_number=1&page_size=100", Token));

        var names = body.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .ToArray();

        Assert.Contains(globalName, names);
        Assert.Contains(ownExercise.GetProperty("name").GetString(), names);
        Assert.DoesNotContain("Other tenant private", names);
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
            $"/api/v1/exercises?page_number=1&page_size={pageSize}",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // page_size=0 é reposto para 50 pelo controller antes de chegar ao validador.
        var expected = pageSize == 0 ? 50 : pageSize;
        var body = await ReadJsonAsync(response);
        Assert.Equal(expected, body.GetProperty("page_size").GetInt32());
    }

    [Fact]
    public async Task List_WithPageSizeAboveLimit_ReturnsValidationProblemDetails()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await client.GetAsync(
            "/api/v1/exercises?page_number=1&page_size=101",
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_StaysWithinQueryBudget()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        for (var index = 0; index < 10; index++)
        {
            await TrainingTestData.SeedPrivateExerciseAsync(
                _fixture.Factory, tenant.TrainerId, $"Exercise {index}", Token);
        }

        var client = TrainerClient(tenant.TrainerId);

        using var scope = CommandCountingInterceptor.BeginScope();
        var response = await client.GetAsync(
            "/api/v1/exercises?page_number=1&page_size=50",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            scope.Count <= 2,
            $"Listing used {scope.Count} commands: {string.Join(" | ", scope.Commands)}");
    }

    private HttpClient TrainerClient(Guid trainerId) =>
        _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainerId));

    private static CreateExerciseRequest NewExercise(string name) =>
        new(name, null, "chest", "barbell", "intermediate", null);

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync(Token);
        return JsonDocument.Parse(payload).RootElement.Clone();
    }
}
