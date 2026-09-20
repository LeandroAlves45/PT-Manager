using System.Net;
using System.Text.Json;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

[Collection(ApiTestCollection.Name)]
public sealed class ClientPortalReadControllerTests : ApiReadEndpointsTestContext
{
    public ClientPortalReadControllerTests(PostgresApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task WorkoutToday_ReturnsPlanAndStatusConsistentWithLocalDate()
    {
        var token = TestContext.Current.CancellationToken;
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, token);

        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId));

        var response = await client.GetAsync("/api/v1/portal/my-workout/today", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var root = document.RootElement;

        Assert.Equal("Portal plan", root.GetProperty("plan_name").GetString());

        // O plano semeado só tem treino à terça (day_of_week = 1) e repete-se todas as semanas.
        var localDate = DateOnly.Parse(
            root.GetProperty("local_date").GetString()!,
            System.Globalization.CultureInfo.InvariantCulture);
        var isTuesday = localDate.DayOfWeek == DayOfWeek.Tuesday;
        var status = root.GetProperty("status").GetString();

        Assert.Equal(isTuesday ? "workout" : "rest", status);

        if (isTuesday)
        {
            Assert.Equal(1, root.GetProperty("progress").GetProperty("planned_sets").GetInt32());
            Assert.Equal(0, root.GetProperty("progress").GetProperty("logged_sets").GetInt32());
        }
        else
        {
            Assert.Equal(
                DayOfWeek.Tuesday,
                DateOnly.Parse(
                    root.GetProperty("next_workout").GetProperty("date").GetString()!,
                    System.Globalization.CultureInfo.InvariantCulture).DayOfWeek);
        }
    }

    [Fact]
    public async Task PortalHome_ReturnsEveryCardInOneRequest()
    {
        var token = TestContext.Current.CancellationToken;
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, token);

        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId));

        var response = await client.GetAsync("/api/v1/portal/home", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("local_date", out _));
        Assert.False(root.GetProperty("workout").ValueKind == JsonValueKind.Null);
        Assert.True(root.GetProperty("nutrition").GetProperty("target_kcal").GetDecimal() > 0);
        Assert.Equal(1, root.GetProperty("supplements").GetProperty("total_count").GetInt32());
        Assert.Equal(0, root.GetProperty("supplements").GetProperty("taken_count").GetInt32());
        Assert.True(root.GetProperty("next_check_in").GetProperty("is_today").GetBoolean());
    }

    [Fact]
    public async Task PortalReads_StayWithinCommandBudget()
    {
        var token = TestContext.Current.CancellationToken;
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientAsync(
            _fixture.Factory, token);

        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId));

        using (var scope = CommandCountingInterceptor.BeginScope())
        {
            var response = await client.GetAsync("/api/v1/portal/my-workout/today", token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(scope.Count <= 4, $"my-workout/today used {scope.Count} commands.");
        }

        using (var scope = CommandCountingInterceptor.BeginScope())
        {
            var response = await client.GetAsync("/api/v1/portal/home", token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(scope.Count <= 8, $"portal/home used {scope.Count} commands.");
        }

        using (var scope = CommandCountingInterceptor.BeginScope())
        {
            var response = await client.GetAsync("/api/v1/portal/my-check-ins/next", token);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(scope.Count <= 2, $"my-check-ins/next used {scope.Count} commands.");
        }
    }

    [Fact]
    public async Task NextCheckIn_WithoutScheduledCheckIn_ReturnsNotFoundProblemDetails()
    {
        var token = TestContext.Current.CancellationToken;
        var (trainerId, clientUserId) = await PortalTestData.SeedActiveClientWithoutCheckInAsync(
            _fixture.Factory, token);
        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId));

        var response = await client.GetAsync("/api/v1/portal/my-check-ins/next", token);
        var body = await response.Content.ReadAsStringAsync(token);

        Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("detail").GetString()));
    }
}
