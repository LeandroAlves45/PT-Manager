using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Contracts.Assessments;
using Api.Contracts.Portal;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>Prova os nove endpoints do portal do cliente autenticado.</summary>
[Collection(ApiTestCollection.Name)]
public sealed class ClientPortalControllerTests
{
    private readonly PostgresApiFixture _fixture;

    public ClientPortalControllerTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetBranding_WithClientToken_ReturnsOk()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, Token);
        var client = ClientPortalClient(trainerId, clientUserId);

        var response = await client.GetAsync("/api/v1/portal/branding", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMyPlan_WithClientToken_ReturnsActivePlan()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, Token);
        var client = ClientPortalClient(trainerId, clientUserId);

        var response = await client.GetAsync("/api/v1/portal/my-plan", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("Portal plan", body.GetProperty("name").GetString());
        Assert.NotEmpty(body.GetProperty("days").EnumerateArray());
    }

    [Fact]
    public async Task GetMyNutrition_WithClientToken_ReturnsActiveMealPlan()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, Token);
        var client = ClientPortalClient(trainerId, clientUserId);

        var response = await client.GetAsync("/api/v1/portal/my-nutrition", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("Portal meal plan", body.GetProperty("name").GetString());
        Assert.NotEmpty(body.GetProperty("meals").EnumerateArray());
    }

    [Fact]
    public async Task GetMyProfile_WithClientToken_ReturnsProfile()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, Token);
        var client = ClientPortalClient(trainerId, clientUserId);

        var response = await client.GetAsync("/api/v1/portal/my-profile", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("Portal client", body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task PatchMyProfile_WithClientToken_UpdatesContactFields()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, Token);
        var client = ClientPortalClient(trainerId, clientUserId);

        var response = await ApiJsonPayload.PatchAsync(
            client,
            "/api/v1/portal/my-profile",
            new UpdateMyProfileRequest(
                "updated.contact@example.test",
                "+351911111111",
                "New contact",
                "+351922222222"),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("updated.contact@example.test", body.GetProperty("contact_email").GetString());
    }

    [Fact]
    public async Task PatchMyProfile_IgnoresNameBirthDateAndSex()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, Token);
        var client = ClientPortalClient(trainerId, clientUserId);

        var payload = """
            {
              "name": "Tampered name",
              "birth_date": "2000-01-01",
              "sex": "male",
              "phone": "+351933333333"
            }
            """;
        using var content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await client.PatchAsync("/api/v1/portal/my-profile", content, Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.Equal("Portal client", body.GetProperty("name").GetString());
        Assert.Equal("1992-05-15", body.GetProperty("birth_date").GetString());
        Assert.Equal("female", body.GetProperty("sex").GetString());
    }

    [Fact]
    public async Task GetMyDueCheckIn_WithDueCheckIn_ReturnsOk()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, Token);
        var client = ClientPortalClient(trainerId, clientUserId);

        var response = await client.GetAsync("/api/v1/portal/my-check-ins/due", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMyDueCheckIn_WithoutDueCheckIn_ReturnsNotFound()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var clientUserId = await PortalTestData.SeedSecondClientSameTrainerAsync(
            _fixture.Factory, tenant.TrainerId, Token);
        var client = ClientPortalClient(tenant.TrainerId, clientUserId);

        var response = await client.GetAsync("/api/v1/portal/my-check-ins/due", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SubmitCheckInResponse_WithClientToken_ReturnsOk()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, Token);
        var client = ClientPortalClient(trainerId, clientUserId);

        var due = await ReadJsonAsync(
            await client.GetAsync("/api/v1/portal/my-check-ins/due", Token));
        var checkInId = due.GetProperty("id").GetGuid();

        var response = await ApiJsonPayload.PostAsync(
            client,
            $"/api/v1/portal/check-ins/{checkInId}/respond",
            new CheckInAnswerRequest(
                71.5m,
                17m,
                "All good",
                null,
                null,
                80,
                75),
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ListMySupplements_WithClientToken_ReturnsPagedAssignments()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, Token);
        var client = ClientPortalClient(trainerId, clientUserId);

        var response = await client.GetAsync("/api/v1/portal/my-supplements", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        Assert.True(body.GetProperty("items").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task GetMySupplement_WithClientToken_ReturnsAssignment()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, Token);
        var assignmentId = await PortalTestData.SeedSupplementAssignmentAsync(
            _fixture.Factory, trainerId, clientUserId, Token);
        var client = ClientPortalClient(trainerId, clientUserId);

        var response = await client.GetAsync(
            $"/api/v1/portal/my-supplements/{assignmentId}",
            Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PortalEndpoints_WithoutToken_ReturnUnauthorized()
    {
        var client = _fixture.Factory.CreateOriginClient();

        var response = await client.GetAsync("/api/v1/portal/my-plan", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyPlan_WithTrainerToken_ReturnsForbidden()
    {
        var (trainerId, _) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, Token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainerId));

        var response = await client.GetAsync("/api/v1/portal/my-plan", Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMyPlan_StaysWithinQueryBudget()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, Token);
        var client = ClientPortalClient(trainerId, clientUserId);

        using var scope = CommandCountingInterceptor.BeginScope();
        var response = await client.GetAsync("/api/v1/portal/my-plan", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            scope.Count <= 1,
            $"Query budget exceeded: {scope.Count} commands. {string.Join(" | ", scope.Commands)}");
    }

    [Fact]
    public async Task GetMyNutrition_StaysWithinQueryBudget()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, Token);
        var client = ClientPortalClient(trainerId, clientUserId);

        using var scope = CommandCountingInterceptor.BeginScope();
        var response = await client.GetAsync("/api/v1/portal/my-nutrition", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            scope.Count <= 1,
            $"Query budget exceeded: {scope.Count} commands. {string.Join(" | ", scope.Commands)}");
    }

    [Fact]
    public async Task ListMySupplements_StaysWithinQueryBudget()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, Token);
        var client = ClientPortalClient(trainerId, clientUserId);

        using var scope = CommandCountingInterceptor.BeginScope();
        var response = await client.GetAsync("/api/v1/portal/my-supplements", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            scope.Count <= 2,
            $"Query budget exceeded: {scope.Count} commands. {string.Join(" | ", scope.Commands)}");
    }

    private HttpClient ClientPortalClient(Guid trainerId, Guid clientUserId) =>
        _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId));

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var document = await response.Content.ReadFromJsonAsync<JsonElement>(
            ApiJsonPayload.Options,
            Token);
        return document;
    }
}
