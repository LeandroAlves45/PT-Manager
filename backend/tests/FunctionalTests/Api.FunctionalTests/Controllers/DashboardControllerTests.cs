using System.Net;
using System.Text.Json;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Contrato HTTP do dashboard sobre a API real.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class DashboardControllerTests : ApiReadEndpointsTestContext
{
    public DashboardControllerTests(PostgresApiFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Dashboard_WithTrainerToken_ReturnsEveryBlock()
    {
        var token = TestContext.Current.CancellationToken;
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(
            _fixture.Factory, $"dash-{Guid.NewGuid():N}", token);

        var client = _fixture.Factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainer.TrainerId));

        var response = await client.GetAsync("/api/v1/dashboard", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("local_today", out _));
        Assert.Equal(0, root.GetProperty("active_client_count").GetInt32());
        Assert.Equal(0, root.GetProperty("check_ins_pending_review").GetProperty("total_count").GetInt32());
        Assert.Equal(0, root.GetProperty("packs_ending").GetProperty("total_count").GetInt32());
        Assert.Equal(0, root.GetProperty("plans_expiring").GetProperty("total_count").GetInt32());
        Assert.Equal(0, root.GetProperty("sessions_today").GetProperty("total_count").GetInt32());
        Assert.Equal(
            0,
            root.GetProperty("clients_without_training_plan").GetProperty("total_count").GetInt32());
        Assert.Empty(
            root.GetProperty("pack_sales").GetProperty("current_month").GetProperty("totals")
                .EnumerateArray());
    }

}
