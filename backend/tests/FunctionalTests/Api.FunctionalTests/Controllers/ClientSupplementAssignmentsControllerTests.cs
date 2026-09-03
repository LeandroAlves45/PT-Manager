using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Contracts.Supplements;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>Prova as atribuições de suplementos aos clientes do tenant.</summary>
[Collection(ApiTestCollection.Name)]
public sealed class ClientSupplementAssignmentsControllerTests
{
    private readonly PostgresApiFixture _fixture;

    public ClientSupplementAssignmentsControllerTests(PostgresApiFixture fixture) =>
        _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Assign_WithTrainerToken_ReturnsCreated()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var supplementId = await SeedSupplementAsync(tenant.TrainerId);
        var client = TrainerClient(tenant.TrainerId);

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/supplement-assignments",
            new AssignSupplementRequest(
                tenant.ClientId,
                supplementId,
                "5 g",
                "Daily",
                null),
            Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
    }

    [Fact]
    public async Task Assign_WithoutToken_ReturnsUnauthorized()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var supplementId = await SeedSupplementAsync(tenant.TrainerId);

        var response = await ApiJsonPayload.PostAsync(
            _fixture.Factory.CreateOriginClient(),
            "/api/v1/supplement-assignments",
            new AssignSupplementRequest(
                tenant.ClientId,
                supplementId,
                "5 g",
                "Daily",
                null),
            Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Assign_WithClientToken_ReturnsForbidden()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var supplementId = await SeedSupplementAsync(tenant.TrainerId);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), tenant.TrainerId));

        var response = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/supplement-assignments",
            new AssignSupplementRequest(
                tenant.ClientId,
                supplementId,
                "5 g",
                "Daily",
                null),
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Assign_WithArchivedSupplement_ReturnsConflict()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var trainer = TrainerClient(tenant.TrainerId);
        var created = await ApiJsonPayload.PostAsync(
            trainer,
            "/api/v1/supplements",
            new CreateSupplementRequest(
                "Archived supplement",
                null,
                "grams",
                "5 g",
                "Daily",
                null),
            Token);
        var supplementId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        await trainer.PostAsync(
            $"/api/v1/supplements/{supplementId}/archive",
            content: null,
            Token);

        var response = await ApiJsonPayload.PostAsync(
            trainer,
            "/api/v1/supplement-assignments",
            new AssignSupplementRequest(
                tenant.ClientId,
                supplementId,
                "5 g",
                "Daily",
                null),
            Token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("supplement_inactive", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task List_WithTrainerToken_ReturnsPagedResults()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var supplementId = await SeedSupplementAsync(tenant.TrainerId);
        var client = TrainerClient(tenant.TrainerId);

        await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/supplement-assignments",
            new AssignSupplementRequest(
                tenant.ClientId,
                supplementId,
                "5 g",
                "Daily",
                null),
            Token);

        var response = await client.GetAsync("/api/v1/supplement-assignments", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithAssignmentFromAnotherTenant_ReturnsNotFound()
    {
        var owner = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var supplementId = await SeedSupplementAsync(owner.TrainerId);
        var created = await ApiJsonPayload.PostAsync(
            TrainerClient(owner.TrainerId),
            "/api/v1/supplement-assignments",
            new AssignSupplementRequest(
                owner.ClientId,
                supplementId,
                "5 g",
                "Daily",
                null),
            Token);
        var assignmentId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        var intruder = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var response = await TrainerClient(intruder.TrainerId)
            .GetAsync($"/api/v1/supplement-assignments/{assignmentId}", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_WithTrainerToken_ReturnsOk()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var supplementId = await SeedSupplementAsync(tenant.TrainerId);
        var client = TrainerClient(tenant.TrainerId);
        var created = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/supplement-assignments",
            new AssignSupplementRequest(
                tenant.ClientId,
                supplementId,
                "5 g",
                "Daily",
                null),
            Token);
        var assignmentId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        var response = await ApiJsonPayload.PatchAsync(
            client,
            $"/api/v1/supplement-assignments/{assignmentId}",
            new UpdateSupplementAssignmentRequest(
                "10 g",
                "Evening",
                "Adjusted"),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeactivateThenReactivate_TogglesAssignment()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var supplementId = await SeedSupplementAsync(tenant.TrainerId);
        var client = TrainerClient(tenant.TrainerId);
        var created = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/supplement-assignments",
            new AssignSupplementRequest(
                tenant.ClientId,
                supplementId,
                "5 g",
                "Daily",
                null),
            Token);
        var assignmentId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        var deactivate = await client.PostAsync(
            $"/api/v1/supplement-assignments/{assignmentId}/deactivate",
            content: null,
            Token);
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        var reactivate = await client.PostAsync(
            $"/api/v1/supplement-assignments/{assignmentId}/reactivate",
            content: null,
            Token);
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
    }

    [Fact]
    public async Task ArchiveSupplement_KeepsAssignmentReadableWithArchivedFlag()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var client = TrainerClient(tenant.TrainerId);
        var supplement = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/supplements",
            new CreateSupplementRequest(
                "Assigned supplement",
                null,
                "grams",
                "5 g",
                "Daily",
                null),
            Token);
        var supplementId = (await ReadJsonAsync(supplement)).GetProperty("id").GetGuid();

        var created = await ApiJsonPayload.PostAsync(
            client,
            "/api/v1/supplement-assignments",
            new AssignSupplementRequest(
                tenant.ClientId,
                supplementId,
                "5 g",
                "Daily",
                null),
            Token);
        var assignmentId = (await ReadJsonAsync(created)).GetProperty("id").GetGuid();

        await client.PostAsync(
            $"/api/v1/supplements/{supplementId}/archive",
            content: null,
            Token);

        var response = await client.GetAsync(
            $"/api/v1/supplement-assignments/{assignmentId}",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.True(body.GetProperty("is_supplement_archived").GetBoolean());
    }

    private async Task<Guid> SeedSupplementAsync(Guid trainerId)
    {
        var created = await ApiJsonPayload.PostAsync(
            TrainerClient(trainerId),
            "/api/v1/supplements",
            new CreateSupplementRequest(
                $"Supplement {Guid.NewGuid():N}",
                null,
                "grams",
                "5 g",
                "Daily",
                null),
            Token);

        return (await ReadJsonAsync(created)).GetProperty("id").GetGuid();
    }

    private HttpClient TrainerClient(Guid trainerId) =>
        _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainerId));

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ApiJsonPayload.Options, Token);
}
