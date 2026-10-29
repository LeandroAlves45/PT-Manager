using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.FunctionalTests.Support;
using Application.Features.Jobs.Dispatching;
using Domain.Entities.Identity;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Bloqueio de um personal trainer pelo superuser (RN-013, FL-008), de ponta a ponta contra
/// PostgreSQL: corte a cada pedido com motivo e nota, entrada só depois da palavra-passe certa,
/// 7 dias para os clientes com aviso no Início, convites anulados, emails pelo outbox e
/// levantamento que repõe tudo. O transporte do Resend é trocado por um gravador.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class TrainerAccessBlockTests : IDisposable
{
    private const string LoginRoute = "/api/v1/auth/login";

    private readonly RecordingHttpMessageHandler _emails = new();
    private readonly ApiWebApplicationFactory _factory;

    public TrainerAccessBlockTests(PostgresApiFixture fixture)
    {
        _factory = new ApiWebApplicationFactory(fixture.ConnectionString)
        {
            // O cliente tipado tem o nome da interface interna do remetente.
            ConfigureServices = services => services.Configure<HttpClientFactoryOptions>(
                "IRenderedEmailSender",
                options => options.HttpMessageHandlerBuilderActions.Add(
                    builder => builder.PrimaryHandler = _emails))
        };
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Block_CutsTheOpenTrainerSessionAtTheNextRequest_WithReasonAndNote()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, "block-cut", Token);
        var api = TrainerClient(trainer.TrainerId);
        var before = await api.GetAsync("/api/v1/clients", Token);

        var block = await BlockAsync(trainer.TrainerId, "malicious_content", "Conteúdo copiado de terceiros.");
        var after = await api.GetAsync("/api/v1/clients", Token);

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.OK, block.StatusCode);
        var card = (await ReadJsonAsync(block)).GetProperty("access_block");
        Assert.Equal("malicious_content", card.GetProperty("reason").GetString());
        Assert.Equal("Conteúdo copiado de terceiros.", card.GetProperty("note").GetString());
        Assert.Equal(
            card.GetProperty("blocked_at").GetDateTime().AddDays(7),
            card.GetProperty("client_access_ends_at").GetDateTime());

        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
        var problem = await ReadJsonAsync(after);
        Assert.Equal("trainer_account_blocked", problem.GetProperty("title").GetString());
        Assert.Equal("malicious_content", problem.GetProperty("block_reason").GetString());
        Assert.Equal("Conteúdo copiado de terceiros.", problem.GetProperty("block_note").GetString());
    }

    [Fact]
    public async Task Login_OfABlockedTrainer_ShowsReasonAndNoteOnlyWithTheRightPassword()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, "block-login", Token);
        await SetPasswordAsync(trainer.TrainerId);
        await BlockAsync(trainer.TrainerId, "prohibited_content", null);
        var anonymous = _factory.CreateOriginClient();

        var wrong = await ApiJsonPayload.PostAsync(
            anonymous, LoginRoute, new { email = trainer.Email, password = "Wrong-Password-1!" }, Token);
        var right = await ApiJsonPayload.PostAsync(
            anonymous, LoginRoute, new { email = trainer.Email, password = AuthTestData.Password }, Token);

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        var wrongProblem = await ReadJsonAsync(wrong);
        Assert.Equal("authentication_invalid_credentials", wrongProblem.GetProperty("title").GetString());
        Assert.False(wrongProblem.TryGetProperty("block_reason", out _));

        Assert.Equal(HttpStatusCode.Forbidden, right.StatusCode);
        var problem = await ReadJsonAsync(right);
        Assert.Equal("trainer_account_blocked", problem.GetProperty("title").GetString());
        Assert.Equal("prohibited_content", problem.GetProperty("block_reason").GetString());
        Assert.Equal(JsonValueKind.Null, problem.GetProperty("block_note").ValueKind);
        // A entrada recusada não deixa sessão nem cookie de refresh.
        Assert.False(right.Headers.Contains("Set-Cookie"));
        Assert.Equal(0, await CountAsync(db => db.RefreshTokens
            .CountAsync(token => token.UserId == trainer.TrainerId && token.RevokedAt == null, Token)));
    }

    [Fact]
    public async Task Block_EndsTheTrainersSessions_ButKeepsTheClientsForTheSevenDays()
    {
        var (trainerId, clientUserId, _) = await PortalTestData.SeedNewClientAsync(_factory, Token);
        await SeedRefreshTokenAsync(trainerId);
        await SeedRefreshTokenAsync(clientUserId);

        await BlockAsync(trainerId, "dangerous_information", null);

        Assert.Equal(0, await CountAsync(db => db.RefreshTokens
            .CountAsync(token => token.UserId == trainerId && token.RevokedAt == null, Token)));
        Assert.Equal(1, await CountAsync(db => db.RefreshTokens
            .CountAsync(token => token.UserId == clientUserId && token.RevokedAt == null, Token)));
    }

    [Fact]
    public async Task ClientOfABlockedTrainer_KeepsThePortalWithANotice_ThenLosesIt_AndUnblockGivesItBack()
    {
        var (trainerId, clientUserId, _) = await PortalTestData.SeedNewClientAsync(_factory, Token);
        var portal = PortalClient(trainerId, clientUserId);
        var block = await ReadJsonAsync(await BlockAsync(trainerId, "malicious_content", "Nota privada"));
        var endsAt = block.GetProperty("access_block").GetProperty("client_access_ends_at").GetDateTime();

        var during = await portal.GetAsync("/api/v1/portal/home", Token);
        var duringBody = await ReadJsonAsync(during);
        var otherPage = await portal.GetAsync("/api/v1/portal/my-supplements/intakes/today", Token);

        // Os 7 dias passaram sem o bloqueio ser levantado.
        await ExecuteAsync(db => db.TrainerAccessBlocks
            .Where(item => item.TrainerId == trainerId)
            .ExecuteUpdateAsync(set => set.SetProperty(item => item.BlockedAt, DateTime.UtcNow.AddDays(-8)), Token));
        var ended = await portal.GetAsync("/api/v1/portal/home", Token);

        var unblock = await AdminClient(await SeedSuperuserAsync())
            .DeleteAsync($"/api/v1/admin/trainers/{trainerId}/access-block", Token);
        var restored = await portal.GetAsync("/api/v1/portal/home", Token);

        Assert.Equal(HttpStatusCode.OK, during.StatusCode);
        var notice = duringBody.GetProperty("access_notice");
        Assert.Equal(endsAt, notice.GetProperty("ends_at").GetDateTime());
        Assert.Equal("Europe/Lisbon", notice.GetProperty("time_zone").GetString());
        // O cliente nunca vê o motivo nem a nota.
        Assert.DoesNotContain("malicious_content", duringBody.GetRawText());
        Assert.DoesNotContain("Nota privada", duringBody.GetRawText());
        Assert.Equal(HttpStatusCode.OK, otherPage.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, ended.StatusCode);
        var endedProblem = await ReadJsonAsync(ended);
        Assert.Equal("client_access_period_ended", endedProblem.GetProperty("title").GetString());
        Assert.False(endedProblem.TryGetProperty("block_reason", out _));

        Assert.Equal(HttpStatusCode.OK, unblock.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await ReadJsonAsync(unblock)).GetProperty("access_block").ValueKind);
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await ReadJsonAsync(restored)).GetProperty("access_notice").ValueKind);
    }

    [Fact]
    public async Task Block_VoidsPendingClientInvitations_AndUnblockDoesNotBringThemBack()
    {
        var invitation = await AuthTestData.SeedInvitationAsync(_factory, "block-invite");

        await BlockAsync(invitation.TrainerId, "malicious_content", null);
        await AdminClient(await SeedSuperuserAsync())
            .DeleteAsync($"/api/v1/admin/trainers/{invitation.TrainerId}/access-block", Token);
        var register = await ApiJsonPayload.PostAsync(
            _factory.CreateOriginClient(),
            "/api/v1/auth/accept-invite/register",
            new { token = invitation.RawToken, password = AuthTestData.Password, confirm_password = AuthTestData.Password },
            Token);

        Assert.Equal(0, await CountAsync(db => db.InviteTokens.IgnoreQueryFilters()
            .CountAsync(token => token.TrainerId == invitation.TrainerId, Token)));
        // O link diz só que o convite não é válido, sem explicar o bloqueio.
        Assert.Equal(HttpStatusCode.Unauthorized, register.StatusCode);
        Assert.Equal("authentication_invitation_invalid", (await ReadJsonAsync(register)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task BlockingTwice_KeepsTheFirstBlock_AndUnblockWithoutBlockIsHarmless()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, "block-twice", Token);
        var admin = AdminClient(await SeedSuperuserAsync());

        var noBlock = await admin.DeleteAsync($"/api/v1/admin/trainers/{trainer.TrainerId}/access-block", Token);
        var first = await ReadJsonAsync(await BlockAsync(trainer.TrainerId, "malicious_content", "primeira"));
        var second = await ReadJsonAsync(await BlockAsync(trainer.TrainerId, "prohibited_content", "segunda"));

        Assert.Equal(HttpStatusCode.OK, noBlock.StatusCode);
        Assert.Equal(
            first.GetProperty("access_block").GetRawText(),
            second.GetProperty("access_block").GetRawText());
        Assert.Equal(1, await CountAsync(db => db.AdministrativeAuditEntries
            .CountAsync(entry => entry.ResourceId == trainer.TrainerId && entry.Action == "trainer_access_blocked", Token)));
    }

    [Theory]
    [InlineData("not_a_reason", null)]
    [InlineData("malicious_content", "a\u0000b")]
    public async Task Block_WithAnInvalidReasonOrNote_IsBadRequest(string reason, string? note)
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, "block-invalid", Token);

        var response = await BlockAsync(trainer.TrainerId, reason, note);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountAsync(db => db.TrainerAccessBlocks
            .CountAsync(item => item.TrainerId == trainer.TrainerId, Token)));
    }

    [Fact]
    public async Task Block_ByATrainer_IsForbidden()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, "block-forbid", Token);
        var other = await TrainerTenantSeeder.SeedTrainerAsync(_factory, "block-victim", Token);

        var response = await ApiJsonPayload.PostAsync(
            TrainerClient(trainer.TrainerId),
            $"/api/v1/admin/trainers/{other.TrainerId}/access-block",
            new { reason_code = "malicious_content" },
            Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await CountAsync(db => db.TrainerAccessBlocks
            .CountAsync(item => item.TrainerId == other.TrainerId, Token)));
    }

    [Fact]
    public async Task Emails_TheTrainerGetsReasonNoteAndTheClientsEnd_TheClientNeverTheReason_TheArchivedNothing()
    {
        var (trainerId, clientUserId, _) = await PortalTestData.SeedNewClientAsync(_factory, Token);
        var (_, archivedUserId, archivedClientId) = await PortalTestData.SeedNewClientAsync(_factory, Token, trainerId);
        await TrainerClient(trainerId).PostAsync($"/api/v1/clients/{archivedClientId}/archive", null, Token);
        var trainerEmail = await EmailOfAsync(trainerId);
        var clientEmail = await EmailOfAsync(clientUserId);
        var archivedEmail = await EmailOfAsync(archivedUserId);

        await BlockAsync(trainerId, "deliberately_false_information", "Rever as receitas.");
        await ActivateAsync();

        var toTrainer = Assert.Single(SentTo(trainerEmail));
        var toClient = Assert.Single(SentTo(clientEmail));
        Assert.Empty(SentTo(archivedEmail));
        var trainerText = AuthTestData.Text(toTrainer);
        Assert.Contains("Informação deliberadamente falsa", trainerText);
        Assert.Contains("Rever as receitas.", trainerText);
        Assert.Contains("Os teus clientes continuam a entrar no portal até", trainerText);
        var clientText = AuthTestData.Text(toClient);
        Assert.Contains("Europe/Lisbon", clientText);
        Assert.DoesNotContain("falsa", clientText);
        Assert.DoesNotContain("Rever as receitas.", clientText);

        await AdminClient(await SeedSuperuserAsync())
            .DeleteAsync($"/api/v1/admin/trainers/{trainerId}/access-block", Token);
        await ActivateAsync();

        Assert.Equal(2, SentTo(trainerEmail).Count);
        Assert.Equal(2, SentTo(clientEmail).Count);
        Assert.Contains("já podes entrar", AuthTestData.Text(SentTo(clientEmail).Last()), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Email_ToATrainerWithoutClientsThatCanEnter_OnlyHasReasonAndNote()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, "block-alone", Token);

        await BlockAsync(trainer.TrainerId, "malicious_content", null);
        await ActivateAsync();

        var text = AuthTestData.Text(Assert.Single(SentTo(trainer.Email)));
        Assert.Contains("Conteúdo malicioso", text);
        Assert.DoesNotContain("clientes", text);
    }

    [Fact]
    public async Task BlockedEmail_LiftedBeforeItIsSent_IsNotSent()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, "block-lifted", Token);
        await BlockAsync(trainer.TrainerId, "malicious_content", null);
        await AdminClient(await SeedSuperuserAsync())
            .DeleteAsync($"/api/v1/admin/trainers/{trainer.TrainerId}/access-block", Token);

        await ActivateAsync();

        var sent = SentTo(trainer.Email);
        var text = AuthTestData.Text(Assert.Single(sent));
        Assert.Contains("foi levantado", text);
    }

    [Fact]
    public async Task Overview_CountsBlockedAccounts()
    {
        var admin = AdminClient(await SeedSuperuserAsync());
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, "block-count", Token);
        var before = await BlockedCountAsync(admin);

        await BlockAsync(trainer.TrainerId, "malicious_content", null);

        Assert.Equal(before + 1, await BlockedCountAsync(admin));
    }

    private async Task<int> BlockedCountAsync(HttpClient admin) =>
        (await ReadJsonAsync(await admin.GetAsync("/api/v1/admin/overview", Token)))
            .GetProperty("trainers").GetProperty("blocked_account_count").GetInt32();

    private async Task<HttpResponseMessage> BlockAsync(Guid trainerId, string reason, string? note) =>
        await ApiJsonPayload.PostAsync(
            AdminClient(await SeedSuperuserAsync()),
            $"/api/v1/admin/trainers/{trainerId}/access-block",
            new { reason_code = reason, note },
            Token);

    private async Task ActivateAsync() =>
        await _factory.Services.GetRequiredService<IJobDispatchActivation>().ActivateAsync(Token);

    private List<RecordingHttpMessageHandler.RecordedRequest> SentTo(string email) =>
        _emails.Requests.Where(request => AuthTestData.Recipient(request) == email).ToList();

    private async Task<string> EmailOfAsync(Guid userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PtManagerDbContext>().Users
            .IgnoreQueryFilters()
            .Where(user => user.Id == userId)
            .Select(user => user.Email)
            .SingleAsync(Token);
    }

    private async Task<Guid> SeedSuperuserAsync() =>
        await AuthTestData.SeedAccountAsync(_factory, $"admin-{Guid.NewGuid():N}@block.test", role: "superuser");

    private async Task SetPasswordAsync(Guid userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var user = await db.Users.IgnoreQueryFilters().SingleAsync(item => item.Id == userId, Token);
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        if (!user.EmailConfirmed)
            user.ConfirmEmail(DateTime.UtcNow);
        user.SetPasswordHash(hasher.HashPassword(user, AuthTestData.Password), DateTime.UtcNow);
        await db.SaveChangesAsync(Token);
    }

    private async Task SeedRefreshTokenAsync(Guid userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var now = DateTime.UtcNow;
        db.RefreshTokens.Add(new RefreshToken(
            userId,
            Guid.NewGuid(),
            $"{Guid.NewGuid():N}{Guid.NewGuid():N}",
            $"{Guid.NewGuid():N}{Guid.NewGuid():N}",
            null,
            now.AddDays(30),
            now));
        await db.SaveChangesAsync(Token);
    }

    private async Task<int> CountAsync(Func<PtManagerDbContext, Task<int>> count)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await count(scope.ServiceProvider.GetRequiredService<PtManagerDbContext>());
    }

    private async Task ExecuteAsync(Func<PtManagerDbContext, Task<int>> update)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await update(scope.ServiceProvider.GetRequiredService<PtManagerDbContext>());
    }

    private HttpClient AdminClient(Guid superuserId) =>
        _factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueSuperuser(superuserId));

    private HttpClient TrainerClient(Guid trainerId) =>
        _factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueTrainer(trainerId));

    private HttpClient PortalClient(Guid trainerId, Guid clientUserId) =>
        _factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId));

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ApiJsonPayload.Options, Token);
}
