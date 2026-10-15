using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Pedido anónimo de novo link de confirmação. Responde 204 em todos os casos de conta:
/// só o formato do email e o rate limit podem falhar.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class AuthEmailConfirmationRequestTests : IAsyncLifetime
{
    private const string RequestRoute = "/api/v1/auth/email-confirmation/request";
    private const string ConfirmRoute = "/api/v1/auth/confirm-email";
    private const string LoginRoute = "/api/v1/auth/login";

    private readonly PostgresApiFixture _database;
    private ApiWebApplicationFactory _factory = null!;

    public AuthEmailConfirmationRequestTests(PostgresApiFixture database) =>
        _database = database;

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

    [Fact]
    public async Task Request_UnconfirmedAccount_Returns204AndTheEmailedLinkConfirmsIt()
    {
        var email = NewEmail("unconfirmed");
        await AuthTestData.SeedAccountAsync(_factory, email, confirmed: false);
        var client = _factory.CreateOriginClient();

        var response = await RequestAsync(client, email);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var sent = Assert.Single(EmailsTo(email));
        var token = AuthTestData.TokenOf(sent);
        Assert.NotNull(token);

        var confirm = await client.PostAsJsonAsync(
            ConfirmRoute, new { token }, TestContext.Current.CancellationToken);
        var login = await client.PostAsJsonAsync(
            LoginRoute,
            new { email, password = AuthTestData.Password },
            TestContext.Current.CancellationToken);
        Assert.Equal(
            (HttpStatusCode.NoContent, HttpStatusCode.OK),
            (confirm.StatusCode, login.StatusCode));
    }

    [Fact]
    public async Task Request_SecondRequestWithinCooldown_KeepsTheFirstLinkAndSendsNothing()
    {
        var email = NewEmail("replaced");
        await AuthTestData.SeedAccountAsync(_factory, email, confirmed: false);
        var client = _factory.CreateOriginClient();

        var first = await RequestAsync(client, email);
        var second = await RequestAsync(client, email);
        var sent = EmailsTo(email);
        var use = await client.PostAsJsonAsync(
            ConfirmRoute,
            new { token = AuthTestData.TokenOf(sent[0]) },
            TestContext.Current.CancellationToken);

        // A resposta é igual, não há segundo email e o link original continua válido.
        Assert.Equal(
            (HttpStatusCode.NoContent, HttpStatusCode.NoContent, 1, HttpStatusCode.NoContent),
            (first.StatusCode, second.StatusCode, sent.Count, use.StatusCode));
    }

    [Fact]
    public async Task Request_EmailWithDifferentCase_FindsTheAccount()
    {
        var email = NewEmail("case");
        await AuthTestData.SeedAccountAsync(_factory, email, confirmed: false);
        var client = _factory.CreateOriginClient();

        var response = await RequestAsync(client, email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single(EmailsTo(email));
    }

    [Fact]
    public async Task Request_UnknownEmail_Returns204WithoutSendingAnything()
    {
        var email = NewEmail("unknown");
        var client = _factory.CreateOriginClient();

        var response = await RequestAsync(client, email);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(EmailsTo(email));
    }

    [Fact]
    public async Task Request_AlreadyConfirmedAccount_Returns204WithoutSendingAnything()
    {
        var email = NewEmail("confirmed");
        await AuthTestData.SeedAccountAsync(_factory, email, confirmed: true);
        var client = _factory.CreateOriginClient();

        var response = await RequestAsync(client, email);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(EmailsTo(email));
    }

    [Fact]
    public async Task Request_InactiveAccount_Returns204WithoutSendingAnything()
    {
        var email = NewEmail("inactive");
        await AuthTestData.SeedAccountAsync(_factory, email, confirmed: false, active: false);
        var client = _factory.CreateOriginClient();

        var response = await RequestAsync(client, email);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(EmailsTo(email));
    }

    [Fact]
    public async Task Request_WhenDeliveryFails_StillReturns204()
    {
        var email = NewEmail("delivery");
        await AuthTestData.SeedAccountAsync(_factory, email, confirmed: false);
        var client = _factory.CreateOriginClient();
        _factory.EmailRequests.ResponseStatusCode = HttpStatusCode.InternalServerError;
        try
        {
            var response = await RequestAsync(client, email);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
        finally
        {
            _factory.EmailRequests.ResponseStatusCode = HttpStatusCode.OK;
        }
    }

    [Theory]
    [InlineData("", "authentication_email_required")]
    [InlineData("not-an-email", "authentication_email_invalid")]
    [InlineData("a@b", "authentication_email_invalid")]
    public async Task Request_MalformedEmail_Returns400WithAStableCode(string email, string code)
    {
        var client = _factory.CreateOriginClient();

        var response = await RequestAsync(client, email);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Contains(
            document.RootElement.GetProperty("errors").EnumerateArray(),
            error => error.GetProperty("code").GetString() == code);
    }

    [Fact]
    public async Task Request_WithoutOrigin_IsRejected()
    {
        var response = await RequestAsync(_factory.CreateClient(), NewEmail("origin"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Request_FourthRequestFromTheSameClient_IsRateLimited()
    {
        var client = _factory.CreateOriginClient();

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 4; attempt++)
            statuses.Add((await RequestAsync(client, NewEmail($"limit-{attempt}"))).StatusCode);

        Assert.Equal(
            new[]
            {
                HttpStatusCode.NoContent, HttpStatusCode.NoContent, HttpStatusCode.NoContent,
                HttpStatusCode.TooManyRequests
            },
            statuses);
    }

    private static string NewEmail(string discriminator) =>
        $"confirm-request-{discriminator}-{Guid.NewGuid():N}@trainer.test";

    private static Task<HttpResponseMessage> RequestAsync(HttpClient client, string email) =>
        client.PostAsJsonAsync(RequestRoute, new { email }, TestContext.Current.CancellationToken);

    private IReadOnlyList<RecordingHttpMessageHandler.RecordedRequest> EmailsTo(string email) =>
        [.. _factory.EmailRequests.Requests.Where(request =>
            string.Equals(AuthTestData.Recipient(request), email, StringComparison.OrdinalIgnoreCase))];
}
