using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Contracts.Nutrition;
using Api.Contracts.Training;
using Api.FunctionalTests.Support;
using Application.Common.Abstractions;
using Domain.Entities.Assessments;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Prova os contratos do trainer alterados na Sprint 6A: check-in revisto, porção padrão,
/// grupos musculares validados e RPE planeado e registado.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class Sprint6ATrainerWritesTests
{
    private readonly PostgresApiFixture _fixture;

    public Sprint6ATrainerWritesTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ReviewCheckIn_Answered_IsIdempotent()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var checkInId = await SeedAnsweredCheckInAsync(tenant.TrainerId, tenant.ClientId);
        var client = TrainerClient(tenant.TrainerId);

        var first = await client.PostAsync($"/api/v1/check-ins/{checkInId}/review", content: null, Token);
        var second = await client.PostAsync($"/api/v1/check-ins/{checkInId}/review", content: null, Token);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (first.StatusCode, second.StatusCode));
        Assert.Equal(
            (await ReadJsonAsync(first)).GetProperty("reviewed_at").GetDateTime(),
            (await ReadJsonAsync(second)).GetProperty("reviewed_at").GetDateTime());
    }

    [Fact]
    public async Task ReviewCheckIn_Unanswered_ReturnsConflict()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var checkInId = await SeedCheckInAsync(tenant.TrainerId, tenant.ClientId, answered: false);

        var response = await TrainerClient(tenant.TrainerId)
            .PostAsync($"/api/v1/check-ins/{checkInId}/review", content: null, Token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("check_in_not_answered", await response.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task ReviewCheckIn_OtherTenantOrClientToken_IsDenied()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var checkInId = await SeedAnsweredCheckInAsync(owner.TrainerId, owner.ClientId);
        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var clientToken = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), owner.TrainerId));

        var crossTenant = await TrainerClient(intruder.TrainerId)
            .PostAsync($"/api/v1/check-ins/{checkInId}/review", content: null, Token);
        var asClient = await clientToken.PostAsync($"/api/v1/check-ins/{checkInId}/review", content: null, Token);

        Assert.Equal((HttpStatusCode.NotFound, HttpStatusCode.Forbidden), (crossTenant.StatusCode, asClient.StatusCode));
    }

    [Fact]
    public async Task Food_DefaultServing_RoundTripsAndRejectsZero()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var created = await ApiJsonPayload.PostAsync(
            client, "/api/v1/foods",
            new CreateFoodRequest($"Arroz {Guid.NewGuid():N}", null, 7m, 78m, 1m, null, 150m), Token);
        var invalid = await ApiJsonPayload.PostAsync(
            client, "/api/v1/foods",
            new CreateFoodRequest($"Arroz {Guid.NewGuid():N}", null, 7m, 78m, 1m, null, 0m), Token);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(150m, (await ReadJsonAsync(created)).GetProperty("default_serving_grams").GetDecimal());
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("food_default_serving_invalid", await invalid.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task Exercise_MuscleGroups_AreNormalizedOrRejected()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var created = await ApiJsonPayload.PostAsync(
            client, "/api/v1/exercises",
            new CreateExerciseRequest($"Remada {Guid.NewGuid():N}", null, "Lats, back, back", null, null, null), Token);
        var invalid = await ApiJsonPayload.PostAsync(
            client, "/api/v1/exercises",
            new CreateExerciseRequest($"Remada {Guid.NewGuid():N}", null, "costas", null, null, null), Token);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("back,lats", (await ReadJsonAsync(created)).GetProperty("muscle_groups").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("exercise_muscle_groups_invalid", await invalid.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task TrainingPlan_PlannedRpe_RoundTripsThroughStructure()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);
        var request = new CreateTrainingPlanRequest(
            tenant.ClientId, "RPE plan", null, null, null, DateOnly.FromDateTime(DateTime.UtcNow), null,
            new TrainingPlanStructureRequest([
                new TrainingDayRequest(null, 1, 1, null, [
                    new DayExerciseRequest(null, tenant.ExerciseId, 1, null, null, null, [
                        new ExerciseSetRequest(null, 1, 6, 62.5m, 90, 120, 8m)
                    ])
                ])
            ]));

        var response = await ApiJsonPayload.PostAsync(client, "/api/v1/training-plans", request, Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var set = (await ReadJsonAsync(response))
            .GetProperty("days")[0].GetProperty("exercises")[0].GetProperty("sets")[0];
        Assert.Equal(8m, set.GetProperty("planned_rpe").GetDecimal());
    }

    private async Task<Guid> SeedAnsweredCheckInAsync(Guid trainerId, Guid clientId) =>
        await SeedCheckInAsync(trainerId, clientId, answered: true);

    private async Task<Guid> SeedCheckInAsync(Guid trainerId, Guid clientId, bool answered)
    {
        var now = TrainerTenantSeeder.SeedInstant;
        var date = DateOnly.FromDateTime(now);

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider
            .GetRequiredService<ITenantContextInitializer>()
            .Establish(trainerId, trainerId, "trainer", TenantOrigin.System, false);
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var checkIn = new CheckIn(trainerId, clientId, date, null, now);
        if (answered)
            checkIn.SubmitResponse(72m, null, "ok", null, null, 80, 70, date, now);
        context.CheckIns.Add(checkIn);
        await context.SaveChangesAsync(Token);
        return checkIn.Id;
    }

    private HttpClient TrainerClient(Guid trainerId) =>
        _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainerId));

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ApiJsonPayload.Options, Token);
}
