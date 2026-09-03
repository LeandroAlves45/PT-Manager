using System.Net;
using System.Text.Json;
using Api.Contracts.Training;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Prova o contrato de prescrição de planos de treino, incluindo a diferença de
/// âmbito entre <c>PATCH</c> e <c>PUT</c> na mesma rota.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class TrainingPlansControllerTests
{
    private readonly PostgresApiFixture _fixture;

    public TrainingPlansControllerTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_WithTrainerToken_ReturnsCreatedWithStructure()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/training-plans",
            NewPlan(tenant, "Plano A", days: 2, setsPerExercise: 3),
            Token);

        Assert.Equal(
            HttpStatusCode.Created,
            response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await ReadJsonAsync(response);
        Assert.Equal(2, body.GetProperty("days").GetArrayLength());
        Assert.Equal(6, CountSets(body));
    }

    [Fact]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = _fixture.Factory.CreateOriginClient();

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/training-plans",
            NewPlan(tenant, "Sem token", days: 1, setsPerExercise: 1),
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
            "/api/v1/training-plans",
            NewPlan(tenant, "Cliente não prescreve", days: 1, setsPerExercise: 1),
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
            "/api/v1/training-plans",
            NewPlan(tenant, "   ", days: 1, setsPerExercise: 1),
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.NotEmpty(body.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public async Task Create_WithClientFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(intruder.TrainerId);

        // O exercício tem de pertencer ao intruso; o alvo do teste é o cliente alheio.
        var request = NewPlan(
            owner with { ExerciseId = intruder.ExerciseId },
            "Plano sobre cliente alheio",
            days: 1,
            setsPerExercise: 1);

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/training-plans",
            request,
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithPlanFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var planId = await CreatePlanAsync(owner, "Plano do dono", days: 1, setsPerExercise: 1);

        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(intruder.TrainerId);

        var response = await client.GetAsync($"/api/v1/training-plans/{planId}", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Patch_RenamesPlanAndPreservesStructure()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);
        var planId = await CreatePlanAsync(tenant, "Antes do PATCH", days: 2, setsPerExercise: 3);

        var before = await ReadJsonAsync(
            await client.GetAsync($"/api/v1/training-plans/{planId}", Token));
        var daysBefore = before.GetProperty("days").GetArrayLength();
        var setsBefore = CountSets(before);

        var response = await ApiJsonPayload.PatchAsync(
            client,
            $"/api/v1/training-plans/{planId}",
            new UpdateTrainingPlanMetadataRequest(
                "Depois do PATCH",
                "descrição nova",
                "hipertrofia",
                null,
                new DateOnly(2026, 9, 1),
                null),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = await ReadJsonAsync(response);
        Assert.Equal("Depois do PATCH", after.GetProperty("name").GetString());
        Assert.Equal(daysBefore, after.GetProperty("days").GetArrayLength());
        Assert.Equal(setsBefore, CountSets(after));
    }

    [Fact]
    public async Task Put_OmittingADay_RemovesThatDay()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);
        var planId = await CreatePlanAsync(tenant, "Antes do PUT", days: 2, setsPerExercise: 3);

        var before = await ReadJsonAsync(
            await client.GetAsync($"/api/v1/training-plans/{planId}", Token));
        Assert.Equal(2, before.GetProperty("days").GetArrayLength());

        var response = await ApiJsonPayload.PutAsync(
            client,
            $"/api/v1/training-plans/{planId}",
            new ReplaceTrainingPlanRequest(
                "Depois do PUT",
                null,
                null,
                null,
                new DateOnly(2026, 9, 1),
                null,
                Structure(tenant.ExerciseId, days: 1, setsPerExercise: 1)),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = await ReadJsonAsync(response);
        Assert.Equal(1, after.GetProperty("days").GetArrayLength());
        Assert.Equal(1, CountSets(after));
    }

    [Fact]
    public async Task PutStructure_ReplacesStructureAndPreservesHeader()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);
        var planId = await CreatePlanAsync(tenant, "Cabeçalho estável", days: 2, setsPerExercise: 2);

        var response = await ApiJsonPayload.PutAsync(
            client,
            $"/api/v1/training-plans/{planId}/structure",
            new UpdateTrainingPlanStructureRequest(
                Structure(tenant.ExerciseId, days: 1, setsPerExercise: 4)),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var after = await ReadJsonAsync(response);
        Assert.Equal("Cabeçalho estável", after.GetProperty("name").GetString());
        Assert.Equal(1, after.GetProperty("days").GetArrayLength());
        Assert.Equal(4, CountSets(after));
    }

    [Fact]
    public async Task Reactivate_RouteDoesNotExist()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);
        var planId = await CreatePlanAsync(tenant, "Sem reativação", days: 1, setsPerExercise: 1);

        var response = await client.PostAsync(
            $"/api/v1/training-plans/{planId}/reactivate",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Archive_WithPlanFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var planId = await CreatePlanAsync(owner, "Alvo de IDOR", days: 1, setsPerExercise: 1);

        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(intruder.TrainerId);

        var response = await client.PostAsync(
            $"/api/v1/training-plans/{planId}/archive",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Archive_MarksThePlanArchived()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);
        var planId = await CreatePlanAsync(tenant, "A arquivar", days: 1, setsPerExercise: 1);

        var response = await client.PostAsync(
            $"/api/v1/training-plans/{planId}/archive",
            content: null,
            Token);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var archived = await ReadJsonAsync(
            await client.GetAsync($"/api/v1/training-plans/{planId}", Token));
        Assert.True(archived.GetProperty("is_archived").GetBoolean());
    }

    [Fact]
    public async Task List_ReturnsOnlyOwnPlans()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var other = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        await CreatePlanAsync(owner, "Plano visível", days: 1, setsPerExercise: 1);
        await CreatePlanAsync(other, "Plano invisível", days: 1, setsPerExercise: 1);

        var client = TrainerClient(owner.TrainerId);
        var body = await ReadJsonAsync(
            await client.GetAsync("/api/v1/training-plans?page_number=1&page_size=100", Token));

        var names = body.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString())
            .ToArray();

        Assert.Contains("Plano visível", names);
        Assert.DoesNotContain("Plano invisível", names);
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
            $"/api/v1/training-plans?page_number=1&page_size={pageSize}",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task List_WithPageSizeAboveLimit_ReturnsBadRequest()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await client.GetAsync(
            "/api/v1/training-plans?page_number=1&page_size=101",
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_StaysWithinQueryBudget()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);

        // Só existe um plano activo por cliente, por isso cada plano tem o seu.
        // O tier FREE limita o tenant a cinco clientes, e o seed já criou um.
        for (var index = 0; index < 4; index++)
        {
            var clientId = await TrainerTenantSeeder.SeedClientAsync(
                _fixture.Factory, tenant.TrainerId, $"Cliente {index}", Token);
            await CreatePlanAsync(
                tenant with { ClientId = clientId },
                $"Plano {index}",
                days: 1,
                setsPerExercise: 1);
        }

        var client = TrainerClient(tenant.TrainerId);

        using var scope = CommandCountingInterceptor.BeginScope();
        var response = await client.GetAsync(
            "/api/v1/training-plans?page_number=1&page_size=50",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            scope.Count <= 2,
            $"Listagem custou {scope.Count} comandos: {string.Join(" | ", scope.Commands)}");
    }

    [Fact]
    public async Task Get_WithFourDaysAndTwentySets_IsNotNPlusOne()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);
        var planId = await CreatePlanAsync(
            tenant, "Plano grande", days: 4, setsPerExercise: 5);

        using var scope = CommandCountingInterceptor.BeginScope();
        var response = await client.GetAsync($"/api/v1/training-plans/{planId}", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal(4, body.GetProperty("days").GetArrayLength());
        Assert.Equal(20, CountSets(body));
        Assert.True(
            scope.Count <= 2,
            $"Get custou {scope.Count} comandos: {string.Join(" | ", scope.Commands)}");
    }

    private async Task<Guid> CreatePlanAsync(
        TrainingTestData.TrainingTenant tenant,
        string name,
        int days,
        int setsPerExercise)
    {
        var client = TrainerClient(tenant.TrainerId);
        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/training-plans",
            NewPlan(tenant, name, days, setsPerExercise),
            Token);

        response.EnsureSuccessStatusCode();

        var body = await ReadJsonAsync(response);
        return body.GetProperty("id").GetGuid();
    }

    private HttpClient TrainerClient(Guid trainerId) =>
        _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainerId));

    private static CreateTrainingPlanRequest NewPlan(
        TrainingTestData.TrainingTenant tenant,
        string name,
        int days,
        int setsPerExercise) =>
        new(
            tenant.ClientId,
            name,
            null,
            "hipertrofia",
            null,
            new DateOnly(2026, 9, 1),
            null,
            Structure(tenant.ExerciseId, days, setsPerExercise));

    private static TrainingPlanStructureRequest Structure(
        Guid exerciseId,
        int days,
        int setsPerExercise) =>
        new(Enumerable.Range(1, days)
            .Select(day => new TrainingDayRequest(
                null,
                day,
                1,
                null,
                [
                    new DayExerciseRequest(
                        null,
                        exerciseId,
                        1,
                        null,
                        null,
                        null,
                        Enumerable.Range(1, setsPerExercise)
                            .Select(set => new ExerciseSetRequest(
                                null, set, 10, 60m, 60, 90))
                            .ToArray())
                ]))
            .ToArray());

    private static int CountSets(JsonElement plan) =>
        plan.GetProperty("days").EnumerateArray()
            .SelectMany(day => day.GetProperty("exercises").EnumerateArray())
            .Sum(exercise => exercise.GetProperty("sets").GetArrayLength());

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync(Token);
        return JsonDocument.Parse(payload).RootElement.Clone();
    }
}
