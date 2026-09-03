using System.Net;
using System.Text.Json;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>Prova que nenhum campo interno do personal trainer chega ao portal do cliente.</summary>
[Collection(ApiTestCollection.Name)]
public sealed class ClientPortalExposureTests
{
    private readonly PostgresApiFixture _fixture;

    public ClientPortalExposureTests(PostgresApiFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("/api/v1/portal/my-plan")]
    [InlineData("/api/v1/portal/my-nutrition")]
    [InlineData("/api/v1/portal/my-profile")]
    [InlineData("/api/v1/portal/my-check-ins/due")]
    public async Task PortalResponses_NeverExposeTrainerInternalFields(string route)
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, TestContext.Current.CancellationToken);

        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId));

        var response = await client.GetAsync(route, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(body);
        var forbidden = new[]
        {
            "client_id", "needs_review", "is_archived", "objective"
        };

        foreach (var field in forbidden)
        {
            Assert.False(
                ContainsProperty(document.RootElement, field),
                $"Response from {route} exposed internal field '{field}'.");
        }
    }

    [Fact]
    public async Task Portal_WithTrainerToken_ReturnsForbidden()
    {
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(Guid.NewGuid()));

        var response = await client.GetAsync(
            "/api/v1/portal/my-plan", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetMySupplement_WithAssignmentFromAnotherClient_ReturnsNotFound()
    {
        var (trainerId, ownerUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, TestContext.Current.CancellationToken);

        var assignmentId = await PortalTestData.SeedSupplementAssignmentAsync(
            _fixture.Factory, trainerId, ownerUserId,
            TestContext.Current.CancellationToken);

        var intruderUserId = await PortalTestData.SeedSecondClientSameTrainerAsync(
            _fixture.Factory, trainerId, TestContext.Current.CancellationToken);

        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(intruderUserId, trainerId));

        var response = await client.GetAsync(
            $"/api/v1/portal/my-supplements/{assignmentId}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetMyProfile_NeverExposesMedicalConditions()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, TestContext.Current.CancellationToken);

        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId));

        var response = await client.GetAsync(
            "/api/v1/portal/my-profile", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(body);
        Assert.False(
            ContainsProperty(document.RootElement, "medical_conditions"),
            "Portal response exposed medical_conditions on the client profile.");
    }

    [Fact]
    public async Task GetMyPlan_WithBlockedExercise_MasksContentWithoutReason()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, TestContext.Current.CancellationToken);
        await PortalTestData.BlockExerciseInActivePlanAsync(
            _fixture.Factory, trainerId, clientUserId, TestContext.Current.CancellationToken);

        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId));

        var response = await client.GetAsync(
            "/api/v1/portal/my-plan", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(body);
        var exercise = document.RootElement
            .GetProperty("days")[0]
            .GetProperty("exercises")[0];

        Assert.True(exercise.GetProperty("is_unavailable").GetBoolean());
        Assert.Equal(
            "Unavailable exercise",
            exercise.GetProperty("exercise_name").GetString());
        Assert.False(
            ContainsProperty(document.RootElement, "platform_enforcement_reason"),
            "Portal response exposed the administrative moderation reason.");
    }

    [Fact]
    public async Task GetMyNutrition_WithBlockedFood_MasksContentWithoutReason()
    {
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, TestContext.Current.CancellationToken);
        await PortalTestData.BlockFoodInActiveMealPlanAsync(
            _fixture.Factory, trainerId, clientUserId, TestContext.Current.CancellationToken);

        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId));

        var response = await client.GetAsync(
            "/api/v1/portal/my-nutrition", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        using var document = JsonDocument.Parse(body);
        var item = document.RootElement
            .GetProperty("meals")[0]
            .GetProperty("items")[0];

        Assert.True(item.GetProperty("is_unavailable").GetBoolean());
        Assert.Equal(
            "Unavailable food",
            item.GetProperty("food_name").GetString());
        Assert.False(
            ContainsProperty(document.RootElement, "platform_enforcement_reason"),
            "Portal response exposed the administrative moderation reason.");
    }

    private static bool ContainsProperty(JsonElement element, string propertyName)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, propertyName, StringComparison.Ordinal))
                        return true;
                    if (ContainsProperty(property.Value, propertyName))
                        return true;
                }

                return false;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (ContainsProperty(item, propertyName))
                        return true;
                }

                return false;

            default:
                return false;
        }
    }
}
