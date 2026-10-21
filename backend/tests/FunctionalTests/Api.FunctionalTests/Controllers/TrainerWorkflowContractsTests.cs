using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Contracts.Portal;
using Api.Contracts.Supplements;
using Api.Contracts.Training;
using Api.FunctionalTests.Support;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Contratos novos da área do trainer: repetições em intervalo (U5), «gr» → «g» (U60), convite
/// com link e validade (U61) e estado na ficha, e conclusões do treino lidas pelo trainer (U63).
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class TrainerWorkflowContractsTests
{
    private readonly PostgresApiFixture _fixture;

    public TrainerWorkflowContractsTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TrainingPlan_RepsRange_RoundTripsToTrainerAndPortalShape()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await ApiJsonPayload.PostAsync(
            client, "/api/v1/training-plans", PlanWithSet(tenant, 10, 12), Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var set = (await ReadJsonAsync(response))
            .GetProperty("days")[0].GetProperty("exercises")[0].GetProperty("sets")[0];
        Assert.Equal((10, 12), (set.GetProperty("planned_reps").GetInt32(), set.GetProperty("planned_reps_max").GetInt32()));
    }

    [Fact]
    public async Task TrainingPlan_SingleTarget_ReturnsNullMax()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);

        var response = await ApiJsonPayload.PostAsync(
            TrainerClient(tenant.TrainerId), "/api/v1/training-plans", PlanWithSet(tenant, 8, null), Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var set = (await ReadJsonAsync(response))
            .GetProperty("days")[0].GetProperty("exercises")[0].GetProperty("sets")[0];
        Assert.Equal(JsonValueKind.Null, set.GetProperty("planned_reps_max").ValueKind);
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(12, 10)]
    [InlineData(null, 12)]
    public async Task TrainingPlan_InvalidRepsRange_Returns400(int? reps, int? max)
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);

        var response = await ApiJsonPayload.PostAsync(
            TrainerClient(tenant.TrainerId), "/api/v1/training-plans", PlanWithSet(tenant, reps, max), Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("training_planned_reps_range_invalid", await response.Content.ReadAsStringAsync(Token));
    }

    [Fact]
    public async Task Portal_MyPlan_ExposesPlannedRepsMax()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var portal = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId));

        var before = await ReadJsonAsync(await portal.GetAsync("/api/v1/portal/my-plan", Token));
        var seeded = before.GetProperty("days")[0].GetProperty("exercises")[0].GetProperty("sets")[0];
        Assert.Equal(JsonValueKind.Null, seeded.GetProperty("planned_reps_max").ValueKind);
        var setId = seeded.GetProperty("id").GetGuid();
        var reps = seeded.GetProperty("planned_reps").GetInt32();

        // Grava o topo do intervalo diretamente na BD: prova o mapeamento da coluna até ao portal.
        await ExecuteSqlAsync($"UPDATE exercise_sets SET planned_reps_max = {reps + 2} WHERE id = {setId}");
        var after = await ReadJsonAsync(await portal.GetAsync("/api/v1/portal/my-plan", Token));

        var set = after.GetProperty("days")[0].GetProperty("exercises")[0].GetProperty("sets")[0];
        Assert.Equal(reps + 2, set.GetProperty("planned_reps_max").GetInt32());
    }

    [Fact]
    public async Task Database_RejectsRepsMaxNotAboveMinimum()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var portal = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId));
        var plan = await ReadJsonAsync(await portal.GetAsync("/api/v1/portal/my-plan", Token));
        var seeded = plan.GetProperty("days")[0].GetProperty("exercises")[0].GetProperty("sets")[0];
        var setId = seeded.GetProperty("id").GetGuid();
        var reps = seeded.GetProperty("planned_reps").GetInt32();

        // A check constraint reps_range_check é a última barreira, mesmo sem passar pela API.
        var failure = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            ExecuteSqlAsync($"UPDATE exercise_sets SET planned_reps_max = {reps} WHERE id = {setId}"));

        Assert.Equal(("23514", "reps_range_check"), (failure.SqlState, failure.ConstraintName));
    }

    private async Task ExecuteSqlAsync(FormattableString sql)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        await context.Database.ExecuteSqlInterpolatedAsync(sql, Token);
    }

    [Fact]
    public async Task Supplement_GrUnitAndServing_AreStoredAsG()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var created = await ApiJsonPayload.PostAsync(
            client, "/api/v1/supplements",
            new CreateSupplementRequest($"Creatina {Guid.NewGuid():N}", null, " GR ", "5gr", null, null), Token);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var supplement = await ReadJsonAsync(created);
        Assert.Equal(("g", "5g"), (
            supplement.GetProperty("unit_of_measure").GetString(),
            supplement.GetProperty("serving_size").GetString()));

        var assigned = await ApiJsonPayload.PostAsync(
            client, "/api/v1/supplement-assignments",
            new AssignSupplementRequest(
                tenant.ClientId, supplement.GetProperty("id").GetGuid(), "2,5 GR", null, null), Token);

        Assert.Equal(HttpStatusCode.Created, assigned.StatusCode);
        Assert.Equal("2,5 g", (await ReadJsonAsync(assigned)).GetProperty("serving_size").GetString());
    }

    [Fact]
    public async Task InviteClient_ReturnsTheEmailedLink_AndTheFichaFollowsItsLifecycle()
    {
        var email = $"invite-link-{Guid.NewGuid():N}@client.test";
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(
            _fixture.Factory, $"invite-link-{Guid.NewGuid():N}", Token);
        var clientId = await TrainerTenantSeeder.SeedClientAsync(
            _fixture.Factory, trainer.TrainerId, "Link Client", Token, contactEmail: email);
        var client = TrainerClient(trainer.TrainerId);

        var before = await ReadJsonAsync(await client.GetAsync($"/api/v1/clients/{clientId}/invitation", Token));
        Assert.Equal("none", before.GetProperty("state").GetString());

        var invited = await ApiJsonPayload.PostAsync(
            client, "/api/v1/auth/invite-client", new { client_id = clientId, email }, Token);

        Assert.Equal(HttpStatusCode.OK, invited.StatusCode);
        var body = await ReadJsonAsync(invited);
        var inviteUrl = new Uri(body.GetProperty("invite_url").GetString()!);
        Assert.EndsWith("/aceitar-convite", inviteUrl.AbsolutePath, StringComparison.Ordinal);
        var rawToken = Uri.UnescapeDataString(inviteUrl.Query["?token=".Length..]);
        var emailed = AuthTestData.TokenOf(_fixture.Factory.EmailRequests.Requests.Last(request =>
            AuthTestData.Recipient(request) == email));
        Assert.Equal(emailed, rawToken);
        Assert.True(body.GetProperty("email_sent").GetBoolean());
        var expiresAt = body.GetProperty("expires_at").GetDateTime();
        Assert.InRange(expiresAt, DateTime.UtcNow.AddDays(7).AddMinutes(-5), DateTime.UtcNow.AddDays(7).AddMinutes(5));

        var pending = await ReadJsonAsync(await client.GetAsync($"/api/v1/clients/{clientId}/invitation", Token));
        Assert.Equal(("pending", email), (pending.GetProperty("state").GetString(), pending.GetProperty("email").GetString()));

        var registered = await ApiJsonPayload.PostAsync(
            _fixture.Factory.CreateOriginClient(), "/api/v1/auth/accept-invite/register",
            new { token = rawToken, password = AuthTestData.Password, confirm_password = AuthTestData.Password },
            Token);
        Assert.Equal(HttpStatusCode.OK, registered.StatusCode);

        var accepted = await ReadJsonAsync(await client.GetAsync($"/api/v1/clients/{clientId}/invitation", Token));
        Assert.Equal("accepted", accepted.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, accepted.GetProperty("email").ValueKind);
    }

    [Fact]
    public async Task InviteClient_WhenEmailFails_StillReturnsTheLinkAndTheFichaStaysPending()
    {
        // Factory própria: mudar a resposta do fornecedor de email não pode afetar outros testes.
        using var factory = new ApiWebApplicationFactory(_fixture.ConnectionString);
        var email = $"invite-down-{Guid.NewGuid():N}@client.test";
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(
            factory, $"invite-down-{Guid.NewGuid():N}", Token);
        var clientId = await TrainerTenantSeeder.SeedClientAsync(
            factory, trainer.TrainerId, "Email Down", Token, contactEmail: email);
        var client = factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueTrainer(trainer.TrainerId));
        factory.EmailRequests.ResponseStatusCode = HttpStatusCode.InternalServerError;

        var invited = await ApiJsonPayload.PostAsync(
            client, "/api/v1/auth/invite-client", new { client_id = clientId, email }, Token);

        Assert.Equal(HttpStatusCode.OK, invited.StatusCode);
        var body = await ReadJsonAsync(invited);
        Assert.False(body.GetProperty("email_sent").GetBoolean());
        Assert.Contains("token=", body.GetProperty("invite_url").GetString(), StringComparison.Ordinal);
        var state = await ReadJsonAsync(await client.GetAsync($"/api/v1/clients/{clientId}/invitation", Token));
        Assert.Equal("pending", state.GetProperty("state").GetString());
    }

    [Fact]
    public async Task ClientInvitation_ExpiredInvite_IsReportedWithItsDate()
    {
        var expiredAt = DateTime.UtcNow.AddHours(-1);
        var invitation = await AuthTestData.SeedInvitationAsync(
            _fixture.Factory, $"invite-expired-{Guid.NewGuid():N}", expiresAt: expiredAt);

        var state = await ReadJsonAsync(await TrainerClient(invitation.TrainerId)
            .GetAsync($"/api/v1/clients/{invitation.ClientId}/invitation", Token));

        Assert.Equal("expired", state.GetProperty("state").GetString());
        Assert.Equal(invitation.Email, state.GetProperty("email").GetString());
    }

    [Fact]
    public async Task ClientInvitation_OtherTrainersClient_Returns404()
    {
        var invitation = await AuthTestData.SeedInvitationAsync(
            _fixture.Factory, $"invite-foreign-{Guid.NewGuid():N}");
        var intruder = await TrainerTenantSeeder.SeedTrainerAsync(
            _fixture.Factory, $"invite-intruder-{Guid.NewGuid():N}", Token);

        var response = await TrainerClient(intruder.TrainerId)
            .GetAsync($"/api/v1/clients/{invitation.ClientId}/invitation", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WorkoutCompletions_OwnerReadsNote_OtherTrainerSeesNothing()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(_fixture.Factory, Token);
        var portal = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId));
        var plan = await ReadJsonAsync(await portal.GetAsync("/api/v1/portal/my-plan", Token));
        var dayId = plan.GetProperty("days")[0].GetProperty("id").GetGuid();
        var completed = await ApiJsonPayload.PostAsync(
            portal, "/api/v1/portal/workout-completions",
            new CompleteMyWorkoutRequest(dayId, "Joelho a doer na última série"), Token);
        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        var clientId = await ClientIdOfAsync(clientUserId);

        var owner = await ReadJsonAsync(await TrainerClient(trainerId)
            .GetAsync($"/api/v1/workout-completions?client_id={clientId}", Token));
        var intruder = await TrainerTenantSeeder.SeedTrainerAsync(
            _fixture.Factory, $"completion-intruder-{Guid.NewGuid():N}", Token);
        var foreign = await ReadJsonAsync(await TrainerClient(intruder.TrainerId)
            .GetAsync($"/api/v1/workout-completions?client_id={clientId}", Token));

        var item = Assert.Single(owner.GetProperty("items").EnumerateArray());
        Assert.Equal("Joelho a doer na última série", item.GetProperty("notes").GetString());
        Assert.Equal(dayId, item.GetProperty("training_plan_day_id").GetGuid());
        Assert.Empty(foreign.GetProperty("items").EnumerateArray());
    }

    private static CreateTrainingPlanRequest PlanWithSet(TrainingTestData.TrainingTenant tenant, int? reps, int? max) =>
        new(
            tenant.ClientId, $"Plano {Guid.NewGuid():N}", null, null, null, DateOnly.FromDateTime(DateTime.UtcNow), null,
            new TrainingPlanStructureRequest([
                new TrainingDayRequest(null, 1, 1, null, [
                    new DayExerciseRequest(null, tenant.ExerciseId, 1, null, null, null, [
                        new ExerciseSetRequest(null, 1, reps, 40m, 60, 90, null, max)
                    ])
                ])
            ]));

    private async Task<Guid> ClientIdOfAsync(Guid clientUserId)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        return await context.Clients.IgnoreQueryFilters().AsNoTracking()
            .Where(client => client.UserId == clientUserId)
            .Select(client => client.Id)
            .SingleAsync(Token);
    }

    private HttpClient TrainerClient(Guid trainerId) =>
        _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainerId));

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ApiJsonPayload.Options, Token);
}
