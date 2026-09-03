using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Contracts.Assessments;
using Api.FunctionalTests.Support;
using Application.Common.Abstractions;
using Domain.Entities.Assessments;
using Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Controllers;

/// <summary>Prova o contrato HTTP e a máquina de estados dos check-ins do trainer.</summary>
[Collection(ApiTestCollection.Name)]
public sealed class CheckInsControllerTests
{
    private readonly PostgresApiFixture _fixture;

    public CheckInsControllerTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_WithTrainerToken_ReturnsCreatedScheduled()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/check-ins",
            NewCheckIn(tenant.ClientId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7))),
            Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await ReadJsonAsync(response);
        Assert.Equal("scheduled", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);

        var response = await ApiJsonPayload.PostAsync(
            _fixture.Factory.CreateOriginClient(),
            "/api/v1/check-ins",
            NewCheckIn(tenant.ClientId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3))),
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
            "/api/v1/check-ins",
            NewCheckIn(tenant.ClientId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3))),
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithClientFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);

        var response = await ApiJsonPayload.PostAsync(
            TrainerClient(intruder.TrainerId),
            "/api/v1/check-ins",
            NewCheckIn(owner.ClientId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(4))),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task List_WithTrainerToken_ReturnsPagedResults()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/check-ins",
            NewCheckIn(tenant.ClientId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5))),
            Token);

        var response = await client.GetAsync("/api/v1/check-ins", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithCheckInFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var checkInId = await SeedCheckInAsync(
            owner.TrainerId,
            owner.ClientId,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(8)));
        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);

        var response = await TrainerClient(intruder.TrainerId)
            .GetAsync($"/api/v1/check-ins/{checkInId}", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reschedule_MovesScheduledCheckIn()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var checkInId = await SeedCheckInAsync(
            tenant.TrainerId,
            tenant.ClientId,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)));
        var client = TrainerClient(tenant.TrainerId);

        var response = await ApiJsonPayload.PatchAsync(
            client,
            $"/api/v1/check-ins/{checkInId}/reschedule",
            new RescheduleCheckInRequest(
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(12)),
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(26))),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Correct_WhenCheckInIsNotAnswered_ReturnsConflict()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var checkInId = await SeedCheckInAsync(
            tenant.TrainerId,
            tenant.ClientId,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(6)));
        var client = TrainerClient(tenant.TrainerId);

        var response = await ApiJsonPayload.PutAsync(
            client,
            $"/api/v1/check-ins/{checkInId}/answer",
            new CorrectCheckInRequest(
                null,
                70m,
                null,
                null,
                null,
                null,
                null,
                null),
            Token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("check_in_not_answered", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Cancel_WithFutureScheduledCheckIn_ReturnsOk()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var checkInId = await SeedCheckInAsync(
            tenant.TrainerId,
            tenant.ClientId,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(9)));
        var client = TrainerClient(tenant.TrainerId);

        var response = await client.PostAsync(
            $"/api/v1/check-ins/{checkInId}/cancel",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("cancelled", body.GetProperty("status").GetString());
    }

    private static CreateCheckInRequest NewCheckIn(Guid clientId, DateOnly checkInDate) =>
        new(clientId, checkInDate, checkInDate.AddDays(14));

    private async Task<Guid> SeedCheckInAsync(
        Guid trainerId,
        Guid clientId,
        DateOnly checkInDate)
    {
        var now = TrainerTenantSeeder.SeedInstant;

        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider
            .GetRequiredService<ITenantContextInitializer>()
            .Establish(trainerId, trainerId, "trainer", TenantOrigin.System, false);

        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var checkIn = new CheckIn(trainerId, clientId, checkInDate, checkInDate.AddDays(14), now);
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
