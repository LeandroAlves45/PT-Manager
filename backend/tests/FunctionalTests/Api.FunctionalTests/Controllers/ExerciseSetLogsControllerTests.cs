using System.Net;
using System.Text.Json;
using Api.Contracts.Training;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Prova o registo de séries realizadas, incluindo a obrigatoriedade de
/// <c>client_id</c> na listagem.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class ExerciseSetLogsControllerTests
{
    private readonly PostgresApiFixture _fixture;

    public ExerciseSetLogsControllerTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Record_WithTrainerToken_ReturnsCreatedWithLocation()
    {
        var context = await SeedPrescriptionAsync();
        var client = TrainerClient(context.TrainerId);

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/exercise-set-logs",
            NewLog(context.DayExerciseId, setNumber: 1),
            Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await ReadJsonAsync(response);
        Assert.Equal(context.ClientId, body.GetProperty("client_id").GetGuid());
        Assert.Equal(60m, body.GetProperty("weight_kg").GetDecimal());
    }

    [Fact]
    public async Task Record_WithoutToken_ReturnsUnauthorized()
    {
        var context = await SeedPrescriptionAsync();
        var client = _fixture.Factory.CreateOriginClient();

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/exercise-set-logs",
            NewLog(context.DayExerciseId, setNumber: 1),
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Record_WithClientToken_ReturnsForbidden()
    {
        var context = await SeedPrescriptionAsync();
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), context.TrainerId));

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/exercise-set-logs",
            NewLog(context.DayExerciseId, setNumber: 1),
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Record_WithNegativeReps_ReturnsValidationProblemDetails()
    {
        var context = await SeedPrescriptionAsync();
        var client = TrainerClient(context.TrainerId);

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/exercise-set-logs",
            new RegisterExerciseSetLogRequest(
                context.DayExerciseId,
                1,
                60m,
                -5,
                null,
                DateTimeOffset.UtcNow),
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.NotEmpty(body.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public async Task Record_WithPrescriptionFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await SeedPrescriptionAsync();
        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(intruder.TrainerId);

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/exercise-set-logs",
            NewLog(owner.DayExerciseId, setNumber: 1),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Correct_UpdatesTheRecordedValues()
    {
        var context = await SeedPrescriptionAsync();
        var client = TrainerClient(context.TrainerId);
        var logId = await RecordLogAsync(context, setNumber: 1);

        var response = await ApiJsonPayload.PatchAsync(
            client,
            $"/api/v1/exercise-set-logs/{logId}",
            new CorrectExerciseSetLogRequest(
                72.5m,
                8,
                "correcção",
                DateTimeOffset.UtcNow),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal(72.5m, body.GetProperty("weight_kg").GetDecimal());
        Assert.Equal(8, body.GetProperty("reps_done").GetInt32());
    }

    [Fact]
    public async Task Correct_WithLogFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await SeedPrescriptionAsync();
        var logId = await RecordLogAsync(owner, setNumber: 1);

        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(intruder.TrainerId);

        var response = await ApiJsonPayload.PatchAsync(
            client,
            $"/api/v1/exercise-set-logs/{logId}",
            new CorrectExerciseSetLogRequest(10m, 1, null, DateTimeOffset.UtcNow),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_WithoutClientId_ReturnsBadRequestAndListsNothing()
    {
        var context = await SeedPrescriptionAsync();
        await RecordLogAsync(context, setNumber: 1);
        var client = TrainerClient(context.TrainerId);

        var response = await client.GetAsync(
            "/api/v1/exercise-set-logs?page_number=1&page_size=50",
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Contains(
            body.GetProperty("errors").EnumerateArray(),
            error => error.GetProperty("code").GetString() == "training_client_id_required");
    }

    /// <summary>
    /// <c>training_plan_id</c> é opcional: omiti-lo lista o cliente inteiro em vez de
    /// falhar na validação.
    /// </summary>
    [Fact]
    public async Task List_WithoutTrainingPlanId_ReturnsTheClientLogs()
    {
        var context = await SeedPrescriptionAsync();
        await RecordLogAsync(context, setNumber: 1);
        await RecordLogAsync(context, setNumber: 2);
        var client = TrainerClient(context.TrainerId);

        var response = await client.GetAsync(
            $"/api/v1/exercise-set-logs?client_id={context.ClientId}&page_number=1&page_size=50",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal(2, body.GetProperty("total_count").GetInt32());
    }

    [Fact]
    public async Task List_WithClientFromAnotherTenant_ReturnsEmptyPage()
    {
        var owner = await SeedPrescriptionAsync();
        await RecordLogAsync(owner, setNumber: 1);

        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(intruder.TrainerId);

        var response = await client.GetAsync(
            $"/api/v1/exercise-set-logs?client_id={owner.ClientId}&page_number=1&page_size=50",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal(0, body.GetProperty("total_count").GetInt32());
    }

    [Fact]
    public async Task List_WithInvertedDateWindow_ReturnsBadRequest()
    {
        var context = await SeedPrescriptionAsync();
        var client = TrainerClient(context.TrainerId);

        var from = Uri.EscapeDataString(
            DateTimeOffset.UtcNow.ToString("O"));
        var to = Uri.EscapeDataString(
            DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));

        var response = await client.GetAsync(
            $"/api/v1/exercise-set-logs?client_id={context.ClientId}" +
            $"&performed_from={from}&performed_to={to}",
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    public async Task List_WithAcceptedPageSizes_ReturnsOk(int pageSize)
    {
        var context = await SeedPrescriptionAsync();
        var client = TrainerClient(context.TrainerId);

        var response = await client.GetAsync(
            $"/api/v1/exercise-set-logs?client_id={context.ClientId}" +
            $"&page_number=1&page_size={pageSize}",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task List_WithPageSizeAboveLimit_ReturnsBadRequest()
    {
        var context = await SeedPrescriptionAsync();
        var client = TrainerClient(context.TrainerId);

        var response = await client.GetAsync(
            $"/api/v1/exercise-set-logs?client_id={context.ClientId}" +
            "&page_number=1&page_size=101",
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task List_StaysWithinQueryBudget()
    {
        var context = await SeedPrescriptionAsync();
        for (var setNumber = 1; setNumber <= 5; setNumber++)
            await RecordLogAsync(context, setNumber);

        var client = TrainerClient(context.TrainerId);

        using var scope = CommandCountingInterceptor.BeginScope();
        var response = await client.GetAsync(
            $"/api/v1/exercise-set-logs?client_id={context.ClientId}" +
            "&page_number=1&page_size=50",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            scope.Count <= 2,
            $"Listagem custou {scope.Count} comandos: {string.Join(" | ", scope.Commands)}");
    }

    private async Task<Guid> RecordLogAsync(PrescriptionContext context, int setNumber)
    {
        var client = TrainerClient(context.TrainerId);
        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/exercise-set-logs",
            NewLog(context.DayExerciseId, setNumber),
            Token);

        response.EnsureSuccessStatusCode();

        var body = await ReadJsonAsync(response);
        return body.GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Cria um plano com um exercício e cinco séries, e devolve o identificador da
    /// prescrição contra a qual os registos são feitos.
    /// </summary>
    private async Task<PrescriptionContext> SeedPrescriptionAsync()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var structure = new TrainingPlanStructureRequest(
        [
            new TrainingDayRequest(null, 1, 1, null,
            [
                new DayExerciseRequest(null, tenant.ExerciseId, 1, null, null, null,
                    Enumerable.Range(1, 5)
                        .Select(set => new ExerciseSetRequest(null, set, 10, 60m, 60, 90))
                        .ToArray())
            ])
        ]);

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/training-plans",
            new CreateTrainingPlanRequest(
                tenant.ClientId,
                "Plano com registos",
                null,
                "hipertrofia",
                null,
                new DateOnly(2026, 9, 1),
                null,
                structure),
            Token);

        response.EnsureSuccessStatusCode();

        var body = await ReadJsonAsync(response);
        var dayExerciseId = body
            .GetProperty("days")[0]
            .GetProperty("exercises")[0]
            .GetProperty("id")
            .GetGuid();

        return new PrescriptionContext(
            tenant.TrainerId,
            tenant.ClientId,
            body.GetProperty("id").GetGuid(),
            dayExerciseId);
    }

    private HttpClient TrainerClient(Guid trainerId) =>
        _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainerId));

    private static RegisterExerciseSetLogRequest NewLog(Guid dayExerciseId, int setNumber) =>
        new(dayExerciseId, setNumber, 60m, 10, null, DateTimeOffset.UtcNow);

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync(Token);
        return JsonDocument.Parse(payload).RootElement.Clone();
    }

    private sealed record PrescriptionContext(
        Guid TrainerId,
        Guid ClientId,
        Guid TrainingPlanId,
        Guid DayExerciseId);
}
