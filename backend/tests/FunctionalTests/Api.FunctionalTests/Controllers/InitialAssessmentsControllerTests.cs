using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Contracts.Assessments;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>Prova o contrato HTTP da avaliação inicial do cliente.</summary>
[Collection(ApiTestCollection.Name)]
public sealed class InitialAssessmentsControllerTests
{
    private readonly PostgresApiFixture _fixture;

    public InitialAssessmentsControllerTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_WithTrainerToken_ReturnsCreated()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/initial-assessments",
            NewAssessment(tenant.ClientId),
            Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);

        var response = await ApiJsonPayload.PostAsync(
            _fixture.Factory.CreateOriginClient(),
            "/api/v1/initial-assessments",
            NewAssessment(tenant.ClientId),
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
            "/api/v1/initial-assessments",
            NewAssessment(tenant.ClientId),
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithEmptyGoals_ReturnsBadRequest()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/initial-assessments",
            NewAssessment(tenant.ClientId) with { Goals = "   " },
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_WhenAssessmentDoesNotExist_ReturnsNotFound()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);

        var response = await client.GetAsync(
            $"/api/v1/clients/{tenant.ClientId}/initial-assessment",
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithClientFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(intruder.TrainerId);

        var response = await client.GetAsync(
            $"/api/v1/clients/{owner.ClientId}/initial-assessment",
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithTrainerToken_ReturnsOk()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var trainer = TrainerClient(tenant.TrainerId);

        var created = await ApiJsonPayload.PostAsync(
            trainer,
            "/api/v1/initial-assessments",
            NewAssessment(tenant.ClientId),
            Token);
        var assessmentId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        var response = await ApiJsonPayload.PutAsync(
            trainer,
            $"/api/v1/initial-assessments/{assessmentId}",
            UpdateAssessment(),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithAssessmentFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var trainer = TrainerClient(owner.TrainerId);
        var created = await ApiJsonPayload.PostAsync(
            trainer,
            "/api/v1/initial-assessments",
            NewAssessment(owner.ClientId),
            Token);
        var assessmentId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var response = await ApiJsonPayload.PutAsync(
            TrainerClient(intruder.TrainerId),
            $"/api/v1/initial-assessments/{assessmentId}",
            UpdateAssessment(),
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static CreateInitialAssessmentRequest NewAssessment(Guid clientId) => new(
        clientId,
        WeightKg: 70m,
        HeightCm: 175,
        BodyFatPercentage: 20m,
        MedicalConditions: "Asma leve",
        FitnessLevel: "intermediate",
        ActivityLevel: "moderately_active",
        Goals: "Perder gordura",
        Profession: "Designer",
        BodyMeasurements: null,
        NutritionIntake: null);

    private static UpdateInitialAssessmentRequest UpdateAssessment() => new(
        WeightKg: 69m,
        HeightCm: 175,
        BodyFatPercentage: 19m,
        MedicalConditions: "Asma leve",
        FitnessLevel: "intermediate",
        ActivityLevel: "moderately_active",
        Goals: "Perder gordura e ganhar força",
        Profession: "Designer",
        BodyMeasurements: null,
        NutritionIntake: null);

    private HttpClient TrainerClient(Guid trainerId) =>
        _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainerId));

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ApiJsonPayload.Options, Token);
}
