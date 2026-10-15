using System.Net;
using System.Net.Http.Json;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Security;

/// <summary>
/// Os três pedidos anónimos que recebem um email não podem distinguir, pela resposta,
/// um email com conta de um sem conta: mesmo status, mesmo corpo, mesmo tipo de conteúdo.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class AuthEnumerationSafetyTests : IAsyncLifetime
{
    private readonly PostgresApiFixture _database;
    private ApiWebApplicationFactory _factory = null!;

    public AuthEnumerationSafetyTests(PostgresApiFixture database) => _database = database;

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
    [InlineData(true)]
    [InlineData(false)]
    public async Task SignUp_ExistingEmailLooksLikeANewOne(bool existingIsConfirmed)
    {
        var existing = NewEmail("signup-existing");
        await AuthTestData.SeedAccountAsync(_factory, existing, confirmed: existingIsConfirmed);

        var forExisting = await Snapshot(SignUpAsync(existing));
        var forNew = await Snapshot(SignUpAsync(NewEmail("signup-new")));

        Assert.Equal(HttpStatusCode.Accepted, forNew.Status);
        Assert.Equal(forNew, forExisting);
    }

    [Fact]
    public async Task EmailConfirmationRequest_EveryAccountStateLooksLikeAnUnknownEmail()
    {
        var unconfirmed = NewEmail("confirm-unconfirmed");
        var confirmed = NewEmail("confirm-confirmed");
        var inactive = NewEmail("confirm-inactive");
        await AuthTestData.SeedAccountAsync(_factory, unconfirmed, confirmed: false);
        await AuthTestData.SeedAccountAsync(_factory, confirmed, confirmed: true);
        await AuthTestData.SeedAccountAsync(_factory, inactive, confirmed: false, active: false);

        var unknown = await Snapshot(ConfirmationRequestAsync(NewEmail("confirm-unknown")));

        Assert.Equal(HttpStatusCode.NoContent, unknown.Status);
        Assert.Equal(unknown, await Snapshot(ConfirmationRequestAsync(unconfirmed)));
        Assert.Equal(unknown, await Snapshot(ConfirmationRequestAsync(confirmed)));
        Assert.Equal(unknown, await Snapshot(ConfirmationRequestAsync(inactive)));
    }

    [Fact]
    public async Task PasswordResetRequest_ExistingEmailLooksLikeAnUnknownOne()
    {
        var existing = NewEmail("reset-existing");
        await AuthTestData.SeedAccountAsync(_factory, existing, confirmed: true);

        var unknown = await Snapshot(ResetRequestAsync(NewEmail("reset-unknown")));

        Assert.Equal(HttpStatusCode.NoContent, unknown.Status);
        Assert.Equal(unknown, await Snapshot(ResetRequestAsync(existing)));
    }

    private static string NewEmail(string discriminator) =>
        $"enumeration-{discriminator}-{Guid.NewGuid():N}@trainer.test";

    // Um cliente por pedido: cada um tem o seu IP e o seu orçamento nos rate limits.
    private Task<HttpResponseMessage> SignUpAsync(string email) =>
        _factory.CreateOriginClient().PostAsJsonAsync(
            "/api/v1/auth/signup",
            new { email, password = AuthTestData.Password, full_name = "Enumeration Probe" },
            TestContext.Current.CancellationToken);

    private Task<HttpResponseMessage> ConfirmationRequestAsync(string email) =>
        _factory.CreateOriginClient().PostAsJsonAsync(
            "/api/v1/auth/email-confirmation/request",
            new { email },
            TestContext.Current.CancellationToken);

    private Task<HttpResponseMessage> ResetRequestAsync(string email) =>
        _factory.CreateOriginClient().PostAsJsonAsync(
            "/api/v1/auth/password-reset/request",
            new { email },
            TestContext.Current.CancellationToken);

    private static async Task<ResponseSnapshot> Snapshot(Task<HttpResponseMessage> pending)
    {
        using var response = await pending;
        return new ResponseSnapshot(
            response.StatusCode,
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            response.Content.Headers.ContentType?.ToString(),
            response.Headers.CacheControl?.ToString());
    }

    private sealed record ResponseSnapshot(
        HttpStatusCode Status,
        string Body,
        string? ContentType,
        string? CacheControl);
}
