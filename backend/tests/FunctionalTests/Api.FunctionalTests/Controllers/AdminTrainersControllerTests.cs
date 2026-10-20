using System.Net;
using System.Text.Json;
using Api.FunctionalTests.Support;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Página «Treinadores» do superuser (RN-008, RN-018, U49–U52): procura pelo email completo,
/// isenção e convite isento, de ponta a ponta contra o PostgreSQL.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class AdminTrainersControllerTests : IAsyncLifetime
{
    private const string LookupRoute = "/api/v1/admin/trainers/lookup";
    private const string ExemptRoute = "/api/v1/admin/trainers/exempt";
    private const string InvitationsRoute = "/api/v1/admin/trainer-invitations";
    private const string StatusRoute = "/api/v1/auth/trainer-invite/status";
    private const string RegisterRoute = "/api/v1/auth/trainer-invite/register";
    private const string LoginRoute = "/api/v1/auth/login";

    private readonly PostgresApiFixture _database;
    private readonly CapturingLoggerProvider _logs = new();
    private ApiWebApplicationFactory _factory = null!;

    public AdminTrainersControllerTests(PostgresApiFixture database) => _database = database;

    public ValueTask InitializeAsync()
    {
        _factory = new ApiWebApplicationFactory(_database.ConnectionString)
        {
            ConfigureServices = services => services.AddSingleton<ILoggerProvider>(_logs)
        };
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }

    [Theory]
    [InlineData("POST", LookupRoute)]
    [InlineData("GET", ExemptRoute)]
    [InlineData("GET", InvitationsRoute)]
    [InlineData("POST", InvitationsRoute)]
    public async Task AdminRoutes_WithTrainerToken_AreForbidden(string method, string route)
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, "adm-forbid", Token);
        var client = _factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueTrainer(trainer.TrainerId));

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), route)
        {
            Content = method == "POST" ? Json(new { email = "x@y.pt" }) : null
        }, Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminRoutes_WithoutToken_AreUnauthorized()
    {
        var response = await _factory.CreateOriginClient().GetAsync(ExemptRoute, Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("a@b")]
    [InlineData("pt-manager")]
    [InlineData("a\u0000b@x.pt")]
    public async Task Lookup_WithInvalidEmail_IsBadRequestNever500(string email)
    {
        var response = await LookupAsync(await SuperuserAsync(), email);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Lookup_ForADeletedTrainer_IsNotTrainerSoTheAdminIsNotSentToA409()
    {
        var admin = await SuperuserAsync();
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, "adm-deleted", Token);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PtManagerDbContext>().Users
                .IgnoreQueryFilters()
                .Where(user => user.Id == trainer.TrainerId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(user => user.IsDeleted, true), Token);
        }

        using var lookup = await ReadAsync(await LookupAsync(admin, trainer.Email));

        Assert.Equal("not_trainer", lookup.RootElement.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await InviteAsync(admin, trainer.Email)).StatusCode);
    }

    [Fact]
    public async Task Lookup_ReportsTheFourOutcomes()
    {
        var admin = await SuperuserAsync();
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, "adm-lookup", Token);
        await TrainerTenantSeeder.SeedClientAsync(_factory, trainer.TrainerId, "Lookup Client", Token);
        var clientEmail = $"lookup-client-{Guid.NewGuid():N}@client.test";
        await AuthTestData.SeedAccountAsync(_factory, clientEmail, role: "client");

        using var found = await ReadAsync(await LookupAsync(admin, trainer.Email.ToUpperInvariant()));
        var card = found.RootElement.GetProperty("trainer");
        Assert.Equal("found", found.RootElement.GetProperty("status").GetString());
        Assert.Equal(trainer.TrainerId, card.GetProperty("trainer_id").GetGuid());
        Assert.Equal("FREE", card.GetProperty("subscription_tier").GetString());
        Assert.Equal(1, card.GetProperty("active_client_count").GetInt32());
        Assert.False(card.GetProperty("is_exempt_from_billing").GetBoolean());

        using var notTrainer = await ReadAsync(await LookupAsync(admin, clientEmail));
        Assert.Equal("not_trainer", notTrainer.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, notTrainer.RootElement.GetProperty("trainer").ValueKind);

        // Um prefixo de um email existente não encontra nada: a procura é pelo email completo.
        var prefix = trainer.Email[..(trainer.Email.IndexOf('@') - 2)] + "@trainer.test";
        using var notFound = await ReadAsync(await LookupAsync(admin, prefix));
        Assert.Equal("not_found", notFound.RootElement.GetProperty("status").GetString());

        var invited = $"lookup-invited-{Guid.NewGuid():N}@new.test";
        Assert.Equal(HttpStatusCode.Created, (await InviteAsync(admin, invited)).StatusCode);
        using var pending = await ReadAsync(await LookupAsync(admin, invited));
        Assert.Equal("invite_pending", pending.RootElement.GetProperty("status").GetString());
        Assert.Equal(invited, pending.RootElement.GetProperty("invitation").GetProperty("email").GetString());
    }

    [Fact]
    public async Task Exemption_GrantAndRemove_FollowRn008AndAppearInTheExemptList()
    {
        var admin = await SuperuserAsync();
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, "adm-exempt", Token);
        var route = $"/api/v1/admin/trainers/{trainer.TrainerId}/billing-exemption";

        using var granted = await ReadAsync(await admin.PostAsync(route, null, Token));
        Assert.Equal("PRO", granted.RootElement.GetProperty("subscription_tier").GetString());
        Assert.Equal("ACTIVE", granted.RootElement.GetProperty("subscription_status").GetString());
        Assert.True(granted.RootElement.GetProperty("is_exempt_from_billing").GetBoolean());
        var since = granted.RootElement.GetProperty("billing_exempt_since").GetDateTime();

        // Repetir não muda a data de início da isenção.
        using var again = await ReadAsync(await admin.PostAsync(route, null, Token));
        Assert.Equal(since, again.RootElement.GetProperty("billing_exempt_since").GetDateTime());
        Assert.True(await IsInExemptListAsync(admin, trainer.TrainerId));
        Assert.Equal(1, await AuditCountAsync("trainer_billing_exempted", trainer.TrainerId));

        using var removed = await ReadAsync(await admin.DeleteAsync(route, Token));
        Assert.Equal("FREE", removed.RootElement.GetProperty("subscription_tier").GetString());
        Assert.Equal("INACTIVE", removed.RootElement.GetProperty("subscription_status").GetString());
        Assert.False(removed.RootElement.GetProperty("is_exempt_from_billing").GetBoolean());
        Assert.Equal(JsonValueKind.Null, removed.RootElement.GetProperty("billing_exempt_since").ValueKind);
        Assert.False(await IsInExemptListAsync(admin, trainer.TrainerId));
        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync(route, Token)).StatusCode);
    }

    [Fact]
    public async Task Exemption_ForUnknownTrainer_IsNotFound()
    {
        var response = await (await SuperuserAsync()).PostAsync(
            $"/api/v1/admin/trainers/{Guid.NewGuid()}/billing-exemption", null, Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Invitation_FullLifecycle_CreatesAnExemptProTrainerThatCanLogIn()
    {
        var admin = await SuperuserAsync();
        var email = $"invited-{Guid.NewGuid():N}@new.test";

        var created = await InviteAsync(admin, email);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var body = await ReadAsync(created);
        var sentAt = body.RootElement.GetProperty("sent_at").GetDateTime();
        Assert.Equal(TimeSpan.FromDays(7), body.RootElement.GetProperty("expires_at").GetDateTime() - sentAt);
        var rawToken = TokenSentTo(email);

        using var pending = await ReadAsync(await admin.GetAsync(InvitationsRoute, Token));
        Assert.Contains(pending.RootElement.EnumerateArray(), item => item.GetProperty("email").GetString() == email);

        using var status = await StatusAsync(rawToken);
        Assert.Equal("valid", status.RootElement.GetProperty("status").GetString());
        Assert.Equal(email, status.RootElement.GetProperty("email").GetString());

        var anonymous = _factory.CreateOriginClient();
        var register = await RegisterAsync(anonymous, rawToken, "Nova Treinadora");
        Assert.Equal(HttpStatusCode.NoContent, register.StatusCode);

        using var used = await StatusAsync(rawToken);
        Assert.Equal("used", used.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, used.RootElement.GetProperty("email").ValueKind);
        Assert.Equal(HttpStatusCode.Conflict, (await RegisterAsync(anonymous, rawToken, "Outra")).StatusCode);

        var login = await ApiJsonPayload.PostAsync(
            anonymous, LoginRoute, new { email, password = AuthTestData.Password }, Token);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var session = await ReadAsync(login);
        Assert.Equal("trainer", session.RootElement.GetProperty("role").GetString());

        using var lookup = await ReadAsync(await LookupAsync(admin, email));
        var card = lookup.RootElement.GetProperty("trainer");
        Assert.Equal("found", lookup.RootElement.GetProperty("status").GetString());
        Assert.Equal("PRO", card.GetProperty("subscription_tier").GetString());
        Assert.True(card.GetProperty("is_exempt_from_billing").GetBoolean());
        Assert.Equal("Nova Treinadora", card.GetProperty("full_name").GetString());
    }

    [Fact]
    public async Task Invitation_SendingAnotherOrRevoking_MakesTheOldLinkInvalid()
    {
        var admin = await SuperuserAsync();
        var email = $"resend-{Guid.NewGuid():N}@new.test";

        await InviteAsync(admin, email);
        var first = TokenSentTo(email);
        _factory.EmailRequests.Clear();
        using var second = await ReadAsync(await InviteAsync(admin, email));
        var secondToken = TokenSentTo(email);

        using (var old = await StatusAsync(first))
            Assert.Equal("invalid", old.RootElement.GetProperty("status").GetString());
        using (var current = await StatusAsync(secondToken))
            Assert.Equal("valid", current.RootElement.GetProperty("status").GetString());

        var invitationId = second.RootElement.GetProperty("invitation_id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent,
            (await admin.DeleteAsync($"{InvitationsRoute}/{invitationId}", Token)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await admin.DeleteAsync($"{InvitationsRoute}/{invitationId}", Token)).StatusCode);

        using var revoked = await StatusAsync(secondToken);
        Assert.Equal("invalid", revoked.RootElement.GetProperty("status").GetString());
        var register = await RegisterAsync(_factory.CreateOriginClient(), secondToken, "Revogada");
        Assert.Equal(HttpStatusCode.Unauthorized, register.StatusCode);
        Assert.Equal("trainer_invitation_invalid", await TitleAsync(register));
    }

    [Fact]
    public async Task Invitation_ForAnEmailWithAnAccount_IsRefused()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, "adm-invite-dup", Token);

        var response = await InviteAsync(await SuperuserAsync(), trainer.Email);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("trainer_invitation_email_registered", await TitleAsync(response));
        Assert.DoesNotContain(_factory.EmailRequests.Requests, request =>
            AuthTestData.Recipient(request) == trainer.Email);
    }

    [Fact]
    public async Task Invitation_Expired_ReportsExpiredAndRefusesToRegister()
    {
        var rawToken = await SeedExpiredInvitationAsync($"expired-{Guid.NewGuid():N}@new.test");

        using var status = await StatusAsync(rawToken);
        Assert.Equal("expired", status.RootElement.GetProperty("status").GetString());

        var register = await RegisterAsync(_factory.CreateOriginClient(), rawToken, "Tarde");
        Assert.Equal(HttpStatusCode.Unauthorized, register.StatusCode);
        Assert.Equal("trainer_invitation_expired", await TitleAsync(register));
    }

    [Fact]
    public async Task InvitationStatus_ForAnUnknownToken_IsInvalidWithoutEmail()
    {
        using var status = await StatusAsync("not-a-real-token");

        Assert.Equal("invalid", status.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, status.RootElement.GetProperty("email").ValueKind);
    }

    [Fact]
    public async Task Register_WithNullCharacterInName_IsBadRequestAndKeepsTheInvitation()
    {
        var admin = await SuperuserAsync();
        var email = $"nul-{Guid.NewGuid():N}@new.test";
        await InviteAsync(admin, email);
        var rawToken = TokenSentTo(email);

        var register = await RegisterAsync(_factory.CreateOriginClient(), rawToken, "Ana\u0000Silva");

        Assert.Equal(HttpStatusCode.BadRequest, register.StatusCode);
        using var status = await StatusAsync(rawToken);
        Assert.Equal("valid", status.RootElement.GetProperty("status").GetString());
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<HttpClient> SuperuserAsync()
    {
        var id = await AuthTestData.SeedAccountAsync(
            _factory, $"admin-{Guid.NewGuid():N}@ptmanager.test", role: "superuser");
        return _factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueSuperuser(id));
    }

    private static Task<HttpResponseMessage> LookupAsync(HttpClient admin, string email) =>
        ApiJsonPayload.PostAsync(admin, LookupRoute, new { email }, Token);

    private static Task<HttpResponseMessage> InviteAsync(HttpClient admin, string email) =>
        ApiJsonPayload.PostAsync(admin, InvitationsRoute, new { email }, Token);

    private async Task<JsonDocument> StatusAsync(string rawToken)
    {
        var response = await ApiJsonPayload.PostAsync(
            _factory.CreateOriginClient(), StatusRoute, new { token = rawToken }, Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string rawToken, string fullName) =>
        ApiJsonPayload.PostAsync(client, RegisterRoute, new
        {
            token = rawToken,
            full_name = fullName,
            password = AuthTestData.Password,
            confirm_password = AuthTestData.Password
        }, Token);

    private string TokenSentTo(string email) =>
        AuthTestData.TokenOf(_factory.EmailRequests.Requests.Last(request =>
            AuthTestData.Recipient(request) == email))!;

    private async Task<bool> IsInExemptListAsync(HttpClient admin, Guid trainerId)
    {
        // A lista é partilhada pela suite: percorre as páginas em vez de assumir a primeira.
        for (var page = 1; ; page++)
        {
            using var body = await ReadAsync(await admin.GetAsync($"{ExemptRoute}?page_number={page}&page_size=100", Token));
            var items = body.RootElement.GetProperty("items").EnumerateArray().ToList();
            if (items.Any(item => item.GetProperty("trainer_id").GetGuid() == trainerId))
                return true;
            if (items.Count < 100)
                return false;
        }
    }

    private async Task<int> AuditCountAsync(string action, Guid trainerId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var subscriptionId = await context.TrainerSubscriptions.IgnoreQueryFilters()
            .Where(subscription => subscription.TrainerId == trainerId)
            .Select(subscription => subscription.Id)
            .SingleAsync(Token);
        return await context.AdministrativeAuditEntries
            .CountAsync(entry => entry.Action == action && entry.ResourceId == subscriptionId, Token);
    }

    private async Task<string> SeedExpiredInvitationAsync(string email)
    {
        var creator = await AuthTestData.SeedAccountAsync(
            _factory, $"admin-{Guid.NewGuid():N}@ptmanager.test", role: "superuser");

        await using var scope = _factory.Services.CreateAsyncScope();
        var generated = scope.ServiceProvider
            .GetRequiredService<Application.Features.Authentication.Abstractions.IOpaqueTokenService>()
            .Generate();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        context.TrainerInvitations.Add(new TrainerInvitation(
            new EmailAddress(email),
            generated.TokenHash,
            creator,
            DateTime.UtcNow.AddDays(-8)));
        await context.SaveChangesAsync(Token);
        return generated.RawToken;
    }

    private async Task<string?> TitleAsync(HttpResponseMessage response)
    {
        using var body = await ReadAsync(response);
        return body.RootElement.GetProperty("title").GetString();
    }

    private async Task<JsonDocument> ReadAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync(Token);
        Assert.True(
            response.IsSuccessStatusCode || response.StatusCode is HttpStatusCode.Conflict or
                HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest,
            $"{(int)response.StatusCode} {response.RequestMessage?.RequestUri}: {raw}{Environment.NewLine}" +
            string.Join(Environment.NewLine, _logs.Snapshot()
                .Where(entry => entry.Exception is not null)
                .Select(entry => entry.Exception!.ToString())));
        return JsonDocument.Parse(raw);
    }

    private static StringContent Json(object value) =>
        new(JsonSerializer.Serialize(value, ApiJsonPayload.Options), System.Text.Encoding.UTF8, "application/json");
}
