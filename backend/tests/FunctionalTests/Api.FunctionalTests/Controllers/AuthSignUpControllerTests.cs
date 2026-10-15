using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.FunctionalTests.Support;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// O signup responde sempre 202 com o mesmo corpo, exista ou não conta para o email.
/// O que muda é o email enviado, e só quem tem a caixa de correio o vê.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class AuthSignUpControllerTests : IAsyncLifetime
{
    private const string SignUpRoute = "/api/v1/auth/signup";
    private const string ConfirmRoute = "/api/v1/auth/confirm-email";
    private const string LoginRoute = "/api/v1/auth/login";

    private readonly PostgresApiFixture _database;
    private ApiWebApplicationFactory _factory = null!;

    public AuthSignUpControllerTests(PostgresApiFixture database) => _database = database;

    public ValueTask InitializeAsync()
    {
        // Factory por classe: os rate limiters vivem no host.
        _factory = new ApiWebApplicationFactory(_database.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task SignUp_NewEmail_Returns202AndSendsConfirmationToThatEmail()
    {
        var email = NewEmail("new");
        var client = _factory.CreateOriginClient();
        _factory.EmailRequests.Clear();

        var response = await SignUpAsync(client, email);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("confirmation_email_sent", await ReadStatusAsync(response));
        var sent = Assert.Single(EmailsTo(email));
        Assert.Contains("confirmar-email", AuthTestData.Text(sent), StringComparison.Ordinal);
        Assert.NotNull(AuthTestData.TokenOf(sent));
        Assert.True(await UserExistsAsync(email));
    }

    [Fact]
    public async Task SignUp_ThenConfirmingTheEmailedLink_LetsTheTrainerLogIn()
    {
        var email = NewEmail("flow");
        var client = _factory.CreateOriginClient();
        await SignUpAsync(client, email);
        var token = AuthTestData.TokenOf(Assert.Single(EmailsTo(email)))!;

        var beforeConfirm = await client.PostAsJsonAsync(
            LoginRoute,
            new { email, password = AuthTestData.Password },
            TestContext.Current.CancellationToken);
        var confirm = await client.PostAsJsonAsync(
            ConfirmRoute, new { token }, TestContext.Current.CancellationToken);
        var afterConfirm = await client.PostAsJsonAsync(
            LoginRoute,
            new { email, password = AuthTestData.Password },
            TestContext.Current.CancellationToken);

        Assert.Equal(
            (HttpStatusCode.Unauthorized, HttpStatusCode.NoContent, HttpStatusCode.OK),
            (beforeConfirm.StatusCode, confirm.StatusCode, afterConfirm.StatusCode));
    }

    [Fact]
    public async Task SignUp_ExistingConfirmedEmail_Returns202AndSendsAccountAlreadyExistsInstead()
    {
        var email = NewEmail("confirmed");
        await AuthTestData.SeedAccountAsync(_factory, email, confirmed: true);
        var client = _factory.CreateOriginClient();

        var response = await SignUpAsync(client, email);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("confirmation_email_sent", await ReadStatusAsync(response));
        var sent = Assert.Single(EmailsTo(email));
        var text = AuthTestData.Text(sent);
        Assert.Contains("entrar", text, StringComparison.Ordinal);
        Assert.Contains("recuperar-password", text, StringComparison.Ordinal);
        Assert.DoesNotContain("confirmar-email", text, StringComparison.Ordinal);
        Assert.Null(AuthTestData.TokenOf(sent));
    }

    [Fact]
    public async Task SignUp_ExistingUnconfirmedEmail_Returns202AndReissuesTheConfirmation()
    {
        var email = NewEmail("unconfirmed");
        await AuthTestData.SeedAccountAsync(_factory, email, confirmed: false);
        var client = _factory.CreateOriginClient();

        var response = await SignUpAsync(client, email);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var sent = Assert.Single(EmailsTo(email));
        Assert.Contains("confirmar-email", AuthTestData.Text(sent), StringComparison.Ordinal);
        Assert.Equal(1, await PendingConfirmationTokensAsync(email));
    }

    [Fact]
    public async Task SignUp_ExistingEmailWithDifferentCase_IsTreatedAsExisting()
    {
        var email = NewEmail("case");
        await AuthTestData.SeedAccountAsync(_factory, email, confirmed: true);
        var client = _factory.CreateOriginClient();

        var response = await SignUpAsync(client, email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Null(AuthTestData.TokenOf(Assert.Single(EmailsTo(email))));
    }

    [Fact]
    public async Task SignUp_ExistingInactiveAccount_Returns202AndSendsNothing()
    {
        var email = NewEmail("inactive");
        await AuthTestData.SeedAccountAsync(_factory, email, confirmed: false, active: false);
        var client = _factory.CreateOriginClient();

        var response = await SignUpAsync(client, email);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Empty(EmailsTo(email));
    }

    [Fact]
    public async Task SignUp_WeakPassword_IsTheSame400ForNewAndExistingEmails()
    {
        var existing = NewEmail("weak-existing");
        await AuthTestData.SeedAccountAsync(_factory, existing, confirmed: true);
        var client = _factory.CreateOriginClient();

        var forNew = await SignUpAsync(client, NewEmail("weak-new"), "alllowercase");
        var forExisting = await SignUpAsync(client, existing, "alllowercase");

        Assert.Equal(HttpStatusCode.BadRequest, forNew.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, forExisting.StatusCode);
        Assert.Equal(await ErrorCodesAsync(forNew), await ErrorCodesAsync(forExisting));
        Assert.Empty(EmailsTo(existing));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("a@b")]
    public async Task SignUp_MalformedEmail_IsA400NotA500(string email)
    {
        var client = _factory.CreateOriginClient();

        var response = await SignUpAsync(client, email);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SignUp_WhenEmailDeliveryFails_StillReturns202AndKeepsTheAccount()
    {
        var email = NewEmail("delivery");
        var client = _factory.CreateOriginClient();
        _factory.EmailRequests.ResponseStatusCode = HttpStatusCode.InternalServerError;
        try
        {
            var response = await SignUpAsync(client, email);

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            Assert.Equal("confirmation_email_sent", await ReadStatusAsync(response));
            Assert.True(await UserExistsAsync(email));
        }
        finally
        {
            _factory.EmailRequests.ResponseStatusCode = HttpStatusCode.OK;
        }
    }

    [Fact]
    public async Task SignUp_WhenExistingAccountEmailDeliveryFails_StillReturns202()
    {
        var email = NewEmail("delivery-existing");
        await AuthTestData.SeedAccountAsync(_factory, email, confirmed: true);
        var client = _factory.CreateOriginClient();
        _factory.EmailRequests.ResponseStatusCode = HttpStatusCode.ServiceUnavailable;
        try
        {
            var response = await SignUpAsync(client, email);

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        }
        finally
        {
            _factory.EmailRequests.ResponseStatusCode = HttpStatusCode.OK;
        }
    }

    [Fact]
    public async Task SignUp_WithoutOrigin_IsRejected()
    {
        var client = _factory.CreateClient();

        var response = await SignUpAsync(client, NewEmail("origin"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SignUp_FourthRequestFromTheSameClient_IsRateLimited()
    {
        var client = _factory.CreateOriginClient();

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 4; attempt++)
            statuses.Add((await SignUpAsync(client, NewEmail($"limit-{attempt}"))).StatusCode);

        Assert.Equal(
            new[]
            {
                HttpStatusCode.Accepted, HttpStatusCode.Accepted, HttpStatusCode.Accepted,
                HttpStatusCode.TooManyRequests
            },
            statuses);
    }

    private static string NewEmail(string discriminator) =>
        $"signup-{discriminator}-{Guid.NewGuid():N}@trainer.test";

    private static Task<HttpResponseMessage> SignUpAsync(
        HttpClient client,
        string email,
        string password = AuthTestData.Password) =>
        client.PostAsJsonAsync(
            SignUpRoute,
            new { email, password, full_name = "Signup Trainer" },
            TestContext.Current.CancellationToken);

    private IReadOnlyList<RecordingHttpMessageHandler.RecordedRequest> EmailsTo(string email) =>
        [.. _factory.EmailRequests.Requests.Where(request =>
            string.Equals(AuthTestData.Recipient(request), email, StringComparison.OrdinalIgnoreCase))];

    private static async Task<string?> ReadStatusAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.GetProperty("status").GetString();
    }

    private static async Task<string> ErrorCodesAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var codes = document.RootElement.GetProperty("errors").EnumerateArray()
            .Select(error => $"{error.GetProperty("field").GetString()}:{error.GetProperty("code").GetString()}");
        return string.Join('|', codes);
    }

    private async Task<bool> UserExistsAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var normalized = new EmailAddress(email).Normalized;
        return await context.Users.AsNoTracking().AnyAsync(
            user => user.NormalizedEmail == normalized, TestContext.Current.CancellationToken);
    }

    private async Task<int> PendingConfirmationTokensAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var normalized = new EmailAddress(email).Normalized;
        return await context.EmailVerificationTokens.AsNoTracking().CountAsync(
            token => token.ConsumedAt == null &&
                context.Users.Any(user => user.Id == token.UserId && user.NormalizedEmail == normalized),
            TestContext.Current.CancellationToken);
    }
}
