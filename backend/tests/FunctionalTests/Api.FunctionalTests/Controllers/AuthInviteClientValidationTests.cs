using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>Um email que o domínio recusaria é um 400 no convite, nunca um 500.</summary>
[Collection(ApiTestCollection.Name)]
public sealed class AuthInviteClientValidationTests : IAsyncLifetime
{
    private readonly PostgresApiFixture _database;
    private ApiWebApplicationFactory _factory = null!;

    public AuthInviteClientValidationTests(PostgresApiFixture database) => _database = database;

    public ValueTask InitializeAsync()
    {
        _factory = new ApiWebApplicationFactory(_database.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData("a@b")]
    [InlineData("not-an-email")]
    public async Task InviteClient_MalformedEmail_Returns400(string email)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(
            _factory, "invite-validation", cancellationToken);
        var clientId = await TrainerTenantSeeder.SeedClientAsync(
            _factory, trainer.TrainerId, "Invite Target", cancellationToken);
        var client = _factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueTrainer(trainer.TrainerId));

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/invite-client",
            new { client_id = clientId, email },
            cancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Contains(
            document.RootElement.GetProperty("errors").EnumerateArray(),
            error => error.GetProperty("code").GetString() == "authentication_email_invalid");
    }
}
