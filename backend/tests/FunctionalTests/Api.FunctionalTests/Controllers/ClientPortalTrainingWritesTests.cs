using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Contracts.Portal;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Prova o contrato HTTP das séries registadas pelo cliente e da conclusão de treino
/// (DEF-PORTAL-001, Sprint 6A): identidade só do token, titularidade e ausência de dados internos.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class ClientPortalTrainingWritesTests
{
    private readonly PostgresApiFixture _fixture;

    public ClientPortalTrainingWritesTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetMyPlan_ExposesIdentifiersNeededToLogAndPlannedRpe()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var plan = await ReadPlanAsync(PortalClient(trainerId, clientUserId));

        Assert.NotEqual(Guid.Empty, plan.DayId);
        Assert.NotEqual(Guid.Empty, plan.DayExerciseId);
        Assert.Equal(JsonValueKind.Null, plan.FirstSet.GetProperty("planned_rpe").ValueKind);
        Assert.NotEqual(Guid.Empty, plan.FirstSet.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task RecordCorrectAndDeleteOwnSet_FollowsTodayRules()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var client = PortalClient(trainerId, clientUserId);
        var plan = await ReadPlanAsync(client);

        var recorded = await ApiJsonPayload.PostAsync(
            client, "/api/v1/portal/exercise-set-logs",
            new RecordMyExerciseSetLogRequest(plan.DayExerciseId, 1, 62.5m, 8, 8.5m, "Leve"), Token);
        Assert.Equal(HttpStatusCode.OK, recorded.StatusCode);
        var log = await ReadJsonAsync(recorded);
        Assert.Equal(8.5m, log.GetProperty("rpe").GetDecimal());
        Assert.False(log.TryGetProperty("client_id", out _));
        var logId = log.GetProperty("id").GetGuid();

        var corrected = await ApiJsonPayload.PatchAsync(
            client, $"/api/v1/portal/exercise-set-logs/{logId}",
            new CorrectMyExerciseSetLogRequest(65m, 7, 9m, null), Token);
        Assert.Equal(HttpStatusCode.OK, corrected.StatusCode);
        Assert.Equal(65m, (await ReadJsonAsync(corrected)).GetProperty("weight_kg").GetDecimal());

        var deleted = await client.DeleteAsync($"/api/v1/portal/exercise-set-logs/{logId}", Token);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
    }

    [Fact]
    public async Task RecordSet_WithInvalidRpe_ReturnsValidationProblem()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var client = PortalClient(trainerId, clientUserId);
        var plan = await ReadPlanAsync(client);

        var response = await ApiJsonPayload.PostAsync(
            client, "/api/v1/portal/exercise-set-logs",
            new RecordMyExerciseSetLogRequest(plan.DayExerciseId, 1, 60m, 8, 7.25m, null), Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("training_rpe_invalid", await response.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task OtherClientOfSameTrainer_CannotLogCorrectOrDeleteForeignSets()
    {
        var (trainerId, ownerUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var owner = PortalClient(trainerId, ownerUserId);
        var plan = await ReadPlanAsync(owner);
        var recorded = await ReadJsonAsync(await ApiJsonPayload.PostAsync(
            owner, "/api/v1/portal/exercise-set-logs",
            new RecordMyExerciseSetLogRequest(plan.DayExerciseId, 1, 60m, 8, null, null), Token));
        var logId = recorded.GetProperty("id").GetGuid();

        var intruderUserId = await PortalTestData.SeedSecondClientSameTrainerAsync(_fixture.Factory, trainerId, Token);
        var intruder = PortalClient(trainerId, intruderUserId);

        var record = await ApiJsonPayload.PostAsync(
            intruder, "/api/v1/portal/exercise-set-logs",
            new RecordMyExerciseSetLogRequest(plan.DayExerciseId, 1, 60m, 8, null, null), Token);
        var correct = await ApiJsonPayload.PatchAsync(
            intruder, $"/api/v1/portal/exercise-set-logs/{logId}",
            new CorrectMyExerciseSetLogRequest(10m, 1, null, null), Token);
        var delete = await intruder.DeleteAsync($"/api/v1/portal/exercise-set-logs/{logId}", Token);
        var complete = await ApiJsonPayload.PostAsync(
            intruder, "/api/v1/portal/workout-completions",
            new CompleteMyWorkoutRequest(plan.DayId, null), Token);

        Assert.Equal(
            (HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.NotFound),
            (record.StatusCode, correct.StatusCode, delete.StatusCode, complete.StatusCode));
    }

    [Fact]
    public async Task CompleteWorkout_IsIdempotentAndBlocksUnmarkingSets()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var client = PortalClient(trainerId, clientUserId);
        var plan = await ReadPlanAsync(client);
        var logId = (await ReadJsonAsync(await ApiJsonPayload.PostAsync(
            client, "/api/v1/portal/exercise-set-logs",
            new RecordMyExerciseSetLogRequest(plan.DayExerciseId, 1, 60m, 8, null, null), Token)))
            .GetProperty("id").GetGuid();

        var first = await ApiJsonPayload.PostAsync(
            client, "/api/v1/portal/workout-completions", new CompleteMyWorkoutRequest(plan.DayId, "Parcial"), Token);
        var second = await ApiJsonPayload.PostAsync(
            client, "/api/v1/portal/workout-completions", new CompleteMyWorkoutRequest(plan.DayId, null), Token);
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (first.StatusCode, second.StatusCode));
        Assert.Equal(
            (await ReadJsonAsync(first)).GetProperty("id").GetGuid(),
            (await ReadJsonAsync(second)).GetProperty("id").GetGuid());

        var delete = await client.DeleteAsync($"/api/v1/portal/exercise-set-logs/{logId}", Token);

        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
        Assert.Contains("workout_already_completed", await delete.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task CompletedWorkout_FreezesPlanStructureForTrainer()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var portal = PortalClient(trainerId, clientUserId);
        var plan = await ReadPlanAsync(portal);
        await ApiJsonPayload.PostAsync(
            portal, "/api/v1/portal/workout-completions", new CompleteMyWorkoutRequest(plan.DayId, null), Token);

        var trainer = _fixture.Factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueTrainer(trainerId));
        var response = await ApiJsonPayload.PutAsync(
            trainer, $"/api/v1/training-plans/{plan.PlanId}/structure",
            new { structure = new { days = Array.Empty<object>() } }, Token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("training_structure_has_history", await response.Content.ReadAsStringAsync(Token));
    }

    [Theory]
    [InlineData("POST", "/api/v1/portal/exercise-set-logs")]
    [InlineData("POST", "/api/v1/portal/workout-completions")]
    [InlineData("DELETE", "/api/v1/portal/exercise-set-logs/00000000-0000-0000-0000-000000000001")]
    public async Task PortalTrainingWrites_WithTrainerToken_ReturnForbidden(string method, string route)
    {
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(Guid.NewGuid()));

        var response = method == "DELETE"
            ? await client.DeleteAsync(route, Token)
            : await ApiJsonPayload.PostAsync(client, route, new { }, Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient PortalClient(Guid trainerId, Guid clientUserId) =>
        _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId));

    private static async Task<PlanIds> ReadPlanAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/portal/my-plan", Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var plan = await ReadJsonAsync(response);
        var day = plan.GetProperty("days")[0];
        var exercise = day.GetProperty("exercises")[0];
        return new PlanIds(
            plan.GetProperty("id").GetGuid(),
            day.GetProperty("id").GetGuid(),
            exercise.GetProperty("id").GetGuid(),
            exercise.GetProperty("sets")[0]);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ApiJsonPayload.Options, Token);

    private sealed record PlanIds(Guid PlanId, Guid DayId, Guid DayExerciseId, JsonElement FirstSet);
}
