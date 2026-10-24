using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Contracts.Assessments;
using Api.Contracts.Portal;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Avaliação inicial preenchida pelo cliente no portal, revista pelo personal trainer, e a
/// regra que a exige antes de qualquer planeamento. Contra PostgreSQL real.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class InitialAssessmentByClientTests
{
    private readonly PostgresApiFixture _fixture;

    public InitialAssessmentByClientTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Client_FillsAssessmentOnce_ThenItDisappearsFromThePortal()
    {
        var (trainerId, clientUserId, clientId) = await PortalTestData.SeedNewClientAsync(_fixture.Factory, Token);
        var portal = PortalClient(trainerId, clientUserId);

        var before = await ReadJsonAsync(await portal.GetAsync("/api/v1/portal/my-initial-assessment", Token));
        var homeBefore = await ReadJsonAsync(await portal.GetAsync("/api/v1/portal/home", Token));
        var submit = await ApiJsonPayload.PostAsync(
            portal, "/api/v1/portal/my-initial-assessment", ValidRequest(), Token);
        var after = await ReadJsonAsync(await portal.GetAsync("/api/v1/portal/my-initial-assessment", Token));
        var homeAfter = await ReadJsonAsync(await portal.GetAsync("/api/v1/portal/home", Token));
        var again = await ApiJsonPayload.PostAsync(
            portal, "/api/v1/portal/my-initial-assessment", ValidRequest(), Token);

        Assert.True(before.GetProperty("pending").GetBoolean());
        Assert.True(homeBefore.GetProperty("initial_assessment_pending").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, submit.StatusCode);
        Assert.False(after.GetProperty("pending").GetBoolean());
        Assert.False(homeAfter.GetProperty("initial_assessment_pending").GetBoolean());
        // A resposta não traz os dados: o cliente não volta a ver a avaliação.
        Assert.False(after.TryGetProperty("weight_kg", out _));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("initial_assessment_already_exists", await TitleAsync(again));

        var trainer = TrainerClient(trainerId);
        var stored = await ReadJsonAsync(
            await trainer.GetAsync($"/api/v1/clients/{clientId}/initial-assessment", Token));
        Assert.True(stored.GetProperty("submitted_by_client").GetBoolean());
        Assert.Equal(JsonValueKind.Null, stored.GetProperty("reviewed_at").ValueKind);
        Assert.Equal(81.5m, stored.GetProperty("weight_kg").GetDecimal());
        Assert.Equal(92m, stored.GetProperty("body_measurements").GetProperty("waist_cm").GetDecimal());
    }

    [Fact]
    public async Task Client_WhenTrainerAlreadyCreatedIt_IsNotPendingAndCannotSubmit()
    {
        var (trainerId, clientUserId, clientId) = await PortalTestData.SeedNewClientAsync(_fixture.Factory, Token);
        var created = await ApiJsonPayload.PostAsync(
            TrainerClient(trainerId), "/api/v1/initial-assessments", TrainerRequest(clientId), Token);
        var portal = PortalClient(trainerId, clientUserId);

        var status = await ReadJsonAsync(await portal.GetAsync("/api/v1/portal/my-initial-assessment", Token));
        var submit = await ApiJsonPayload.PostAsync(
            portal, "/api/v1/portal/my-initial-assessment", ValidRequest(), Token);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.False(status.GetProperty("pending").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, submit.StatusCode);
    }

    [Fact]
    public async Task Client_NullCharacterInText_IsRefusedWith400()
    {
        var (trainerId, clientUserId, _) = await PortalTestData.SeedNewClientAsync(_fixture.Factory, Token);

        var response = await ApiJsonPayload.PostAsync(
            PortalClient(trainerId, clientUserId),
            "/api/v1/portal/my-initial-assessment",
            ValidRequest() with { Goals = "for\u0000ça" },
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = (await ReadJsonAsync(response)).GetProperty("errors")[0];
        Assert.Equal("text_invalid_character", error.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Client_Archived_CannotReadOrSubmit()
    {
        var (trainerId, clientUserId, clientId) = await PortalTestData.SeedNewClientAsync(_fixture.Factory, Token);
        await TrainerClient(trainerId).PostAsync($"/api/v1/clients/{clientId}/archive", null, Token);
        var portal = PortalClient(trainerId, clientUserId);

        var status = await portal.GetAsync("/api/v1/portal/my-initial-assessment", Token);
        var submit = await ApiJsonPayload.PostAsync(
            portal, "/api/v1/portal/my-initial-assessment", ValidRequest(), Token);

        // Arquivar corta a sessão aberta no pedido seguinte.
        Assert.Equal(HttpStatusCode.Unauthorized, status.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, submit.StatusCode);
        Assert.Equal("authentication_session_ended", await TitleAsync(submit));
    }

    [Fact]
    public async Task Trainer_SeesSubmittedAssessmentToReview_AndMarksItReviewed()
    {
        var (trainerId, clientUserId, clientId) = await PortalTestData.SeedNewClientAsync(_fixture.Factory, Token);
        // Cliente do mesmo personal trainer com avaliação feita por ele: nunca entra na lista.
        var (_, _, trainerMadeClientId) = await PortalTestData.SeedNewClientAsync(
            _fixture.Factory, Token, trainerId);
        var trainer = TrainerClient(trainerId);
        await ApiJsonPayload.PostAsync(
            trainer, "/api/v1/initial-assessments", TrainerRequest(trainerMadeClientId), Token);
        await ApiJsonPayload.PostAsync(
            PortalClient(trainerId, clientUserId), "/api/v1/portal/my-initial-assessment", ValidRequest(), Token);

        var dashboard = (await ReadJsonAsync(await trainer.GetAsync("/api/v1/dashboard", Token)))
            .GetProperty("initial_assessments_pending_review");
        var list = await ReadJsonAsync(
            await trainer.GetAsync("/api/v1/initial-assessments/pending-review", Token));
        var item = Assert.Single(list.GetProperty("items").EnumerateArray());
        var assessmentId = item.GetProperty("assessment_id").GetGuid();

        var reviewed = await trainer.PostAsync($"/api/v1/initial-assessments/{assessmentId}/review", null, Token);
        var reviewedBody = await ReadJsonAsync(reviewed);
        var again = await ReadJsonAsync(
            await trainer.PostAsync($"/api/v1/initial-assessments/{assessmentId}/review", null, Token));
        var listAfter = await ReadJsonAsync(
            await trainer.GetAsync("/api/v1/initial-assessments/pending-review", Token));

        Assert.Equal(1, dashboard.GetProperty("total_count").GetInt32());
        Assert.Equal(clientId, item.GetProperty("client_id").GetGuid());
        Assert.Equal("Cliente novo", item.GetProperty("client_name").GetString());
        Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);
        Assert.NotEqual(JsonValueKind.Null, reviewedBody.GetProperty("reviewed_at").ValueKind);
        // Repetir mantém o instante original (o PostgreSQL guarda microssegundos).
        Assert.Equal(
            reviewedBody.GetProperty("reviewed_at").GetDateTime().Ticks / 10,
            again.GetProperty("reviewed_at").GetDateTime().Ticks / 10);
        Assert.Equal(0, listAfter.GetProperty("total_count").GetInt32());
    }

    [Fact]
    public async Task PendingReview_ArchivedClientLeavesTheList_AndReturnsWhenReactivated()
    {
        var (trainerId, clientUserId, clientId) = await PortalTestData.SeedNewClientAsync(_fixture.Factory, Token);
        await ApiJsonPayload.PostAsync(
            PortalClient(trainerId, clientUserId), "/api/v1/portal/my-initial-assessment", ValidRequest(), Token);
        var trainer = TrainerClient(trainerId);

        await trainer.PostAsync($"/api/v1/clients/{clientId}/archive", null, Token);
        var archived = await ReadJsonAsync(
            await trainer.GetAsync("/api/v1/initial-assessments/pending-review", Token));
        var reactivate = await trainer.PostAsync($"/api/v1/clients/{clientId}/reactivate", null, Token);
        var reactivated = await ReadJsonAsync(
            await trainer.GetAsync("/api/v1/initial-assessments/pending-review", Token));

        Assert.Equal(0, archived.GetProperty("total_count").GetInt32());
        Assert.True(reactivate.IsSuccessStatusCode);
        Assert.Equal(1, reactivated.GetProperty("total_count").GetInt32());
    }

    [Fact]
    public async Task Review_TrainerMadeAssessment_ReturnsConflict()
    {
        var (trainerId, _, clientId) = await PortalTestData.SeedNewClientAsync(_fixture.Factory, Token);
        var trainer = TrainerClient(trainerId);
        var created = await ReadJsonAsync(await ApiJsonPayload.PostAsync(
            trainer, "/api/v1/initial-assessments", TrainerRequest(clientId), Token));

        var response = await trainer.PostAsync(
            $"/api/v1/initial-assessments/{created.GetProperty("id").GetGuid()}/review", null, Token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("initial_assessment_not_reviewable", await TitleAsync(response));
    }

    [Fact]
    public async Task Review_AssessmentOfAnotherTrainer_ReturnsNotFound()
    {
        var (trainerId, clientUserId, _) = await PortalTestData.SeedNewClientAsync(_fixture.Factory, Token);
        await ApiJsonPayload.PostAsync(
            PortalClient(trainerId, clientUserId), "/api/v1/portal/my-initial-assessment", ValidRequest(), Token);
        var ownerList = await ReadJsonAsync(
            await TrainerClient(trainerId).GetAsync("/api/v1/initial-assessments/pending-review", Token));
        var assessmentId = ownerList.GetProperty("items")[0].GetProperty("assessment_id").GetGuid();
        var (intruderId, _, _) = await PortalTestData.SeedNewClientAsync(_fixture.Factory, Token);
        var intruder = TrainerClient(intruderId);

        var review = await intruder.PostAsync($"/api/v1/initial-assessments/{assessmentId}/review", null, Token);
        var intruderList = await ReadJsonAsync(
            await intruder.GetAsync("/api/v1/initial-assessments/pending-review", Token));

        Assert.Equal(HttpStatusCode.NotFound, review.StatusCode);
        Assert.Equal(0, intruderList.GetProperty("total_count").GetInt32());
    }

    [Fact]
    public async Task Update_ArchivedClient_IsRefused()
    {
        var (trainerId, _, clientId) = await PortalTestData.SeedNewClientAsync(_fixture.Factory, Token);
        var trainer = TrainerClient(trainerId);
        var created = await ReadJsonAsync(await ApiJsonPayload.PostAsync(
            trainer, "/api/v1/initial-assessments", TrainerRequest(clientId), Token));
        await trainer.PostAsync($"/api/v1/clients/{clientId}/archive", null, Token);

        var response = await ApiJsonPayload.PutAsync(
            trainer,
            $"/api/v1/initial-assessments/{created.GetProperty("id").GetGuid()}",
            new UpdateInitialAssessmentRequest(
                90m, 180, null, null, "intermediate", "moderately_active", "Força", null, null, null),
            Token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("assessment_client_inactive", await TitleAsync(response));
    }

    [Fact]
    public async Task Planning_WithoutInitialAssessment_IsRefused_AndAllowedAfterTheClientFillsIt()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token, withInitialAssessment: false);
        var trainer = TrainerClient(tenant.TrainerId);
        var checkInDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10));

        var checkIn = await ApiJsonPayload.PostAsync(
            trainer, "/api/v1/check-ins", new CreateCheckInRequest(tenant.ClientId, checkInDate, null), Token);
        var plan = await ApiJsonPayload.PostAsync(
            trainer, "/api/v1/training-plans", TrainingTestData.SimplePlan(tenant), Token);
        // Sessões e packs não esperam pela avaliação inicial.
        var session = await ApiJsonPayload.PostAsync(
            trainer, "/api/v1/sessions", TrainingTestData.SimpleSession(tenant), Token);
        await ApiJsonPayload.PostAsync(
            trainer, "/api/v1/initial-assessments", TrainerRequest(tenant.ClientId), Token);
        var planAfter = await ApiJsonPayload.PostAsync(
            trainer, "/api/v1/training-plans", TrainingTestData.SimplePlan(tenant), Token);

        Assert.Equal(HttpStatusCode.Conflict, checkIn.StatusCode);
        Assert.Equal("initial_assessment_required", await TitleAsync(checkIn));
        Assert.Equal(HttpStatusCode.Conflict, plan.StatusCode);
        Assert.Equal("initial_assessment_required", await TitleAsync(plan));
        Assert.Equal(HttpStatusCode.Created, session.StatusCode);
        Assert.Equal(HttpStatusCode.Created, planAfter.StatusCode);
    }

    [Fact]
    public async Task Planning_ChecksTheAssessmentOfThatClient_NotOfAnotherClientOfTheSameTrainer()
    {
        // O cliente do tenant tem avaliação inicial; o segundo cliente do mesmo personal trainer não.
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var withoutAssessmentId = await TrainerTenantSeeder.SeedClientAsync(
            _fixture.Factory, tenant.TrainerId, "Sem avaliação", Token, withInitialAssessment: false);
        var trainer = TrainerClient(tenant.TrainerId);

        var refused = await ApiJsonPayload.PostAsync(
            trainer,
            "/api/v1/training-plans",
            TrainingTestData.SimplePlan(tenant with { ClientId = withoutAssessmentId }),
            Token);
        var allowed = await ApiJsonPayload.PostAsync(
            trainer, "/api/v1/training-plans", TrainingTestData.SimplePlan(tenant), Token);

        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Equal("initial_assessment_required", await TitleAsync(refused));
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
    }

    private static SubmitMyInitialAssessmentRequest ValidRequest() => new(
        81.5m,
        172,
        22.5m,
        null,
        "beginner",
        "lightly_active",
        "Perder massa gorda",
        "Professora",
        new BodyMeasurementsPayload(92m, 100m, null, null, null, null, null, null, null),
        null);

    private static CreateInitialAssessmentRequest TrainerRequest(Guid clientId) => new(
        clientId, 80m, 180, null, null, "intermediate", "moderately_active", "Força", null, null, null);

    private HttpClient PortalClient(Guid trainerId, Guid clientUserId) =>
        _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId));

    private HttpClient TrainerClient(Guid trainerId) =>
        _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainerId));

    private static async Task<string?> TitleAsync(HttpResponseMessage response) =>
        (await ReadJsonAsync(response)).GetProperty("title").GetString();

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ApiJsonPayload.Options, Token);
}
