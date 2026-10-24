using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Contracts.Authentication;
using Api.FunctionalTests.Support;
using Domain.Entities.Identity;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// O que arquivar um cliente faz além de o tirar das listas: sessões futuras canceladas,
/// saldo dos packs consumido, convite anulado, sessões de login terminadas, listas ativas sem
/// o arquivado. Reativar não devolve nada disto. Contra PostgreSQL real.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class ClientArchiveTests
{
    private readonly PostgresApiFixture _fixture;

    public ClientArchiveTests(PostgresApiFixture fixture) => _fixture = fixture;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Archive_CancelsFutureSessionsAndConsumesTheRemainingBalance_AndReactivateGivesNothingBack()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var packId = await TrainingTestData.SeedClientSessionPackAsync(
            _fixture.Factory, tenant.TrainerId, tenant.ClientId, tenant.PackTypeId, Token);
        var pastId = await TrainingTestData.SeedSessionAsync(
            _fixture.Factory, tenant.TrainerId, tenant.ClientId, packId, DateTimeOffset.UtcNow.AddHours(-3), Token);
        var futureId = await TrainingTestData.SeedSessionAsync(
            _fixture.Factory, tenant.TrainerId, tenant.ClientId, packId, DateTimeOffset.UtcNow.AddDays(2), Token);
        var trainer = TrainerClient(tenant.TrainerId);
        var completed = await trainer.PostAsync($"/api/v1/sessions/{pastId}/complete", null, Token);
        var balanceBefore = await TrainingTestData.ReadPackBalanceAsync(
            _fixture.Factory, tenant.TrainerId, packId, Token);

        var archive = await trainer.PostAsync($"/api/v1/clients/{tenant.ClientId}/archive", null, Token);
        var reactivate = await trainer.PostAsync($"/api/v1/clients/{tenant.ClientId}/reactivate", null, Token);

        Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        Assert.Equal(9, balanceBefore);
        Assert.True(archive.IsSuccessStatusCode);
        Assert.True(reactivate.IsSuccessStatusCode);
        Assert.Equal(0, await TrainingTestData.ReadPackBalanceAsync(
            _fixture.Factory, tenant.TrainerId, packId, Token));
        Assert.Equal("cancelled_by_trainer", await SessionStatusAsync(trainer, futureId));
        // O passado fica como estava.
        Assert.Equal("completed", await SessionStatusAsync(trainer, pastId));
    }

    [Fact]
    public async Task RestoringASessionOfAPackClosedByTheArchive_DoesNotGiveTheBalanceBack()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var packId = await TrainingTestData.SeedClientSessionPackAsync(
            _fixture.Factory, tenant.TrainerId, tenant.ClientId, tenant.PackTypeId, Token);
        var pastId = await TrainingTestData.SeedSessionAsync(
            _fixture.Factory, tenant.TrainerId, tenant.ClientId, packId, DateTimeOffset.UtcNow.AddHours(-3), Token);
        var trainer = TrainerClient(tenant.TrainerId);
        await trainer.PostAsync($"/api/v1/sessions/{pastId}/complete", null, Token);
        await trainer.PostAsync($"/api/v1/clients/{tenant.ClientId}/archive", null, Token);
        await trainer.PostAsync($"/api/v1/clients/{tenant.ClientId}/reactivate", null, Token);

        var restore = await trainer.PostAsync($"/api/v1/sessions/{pastId}/restore", null, Token);

        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        Assert.Equal("scheduled", await SessionStatusAsync(trainer, pastId));
        Assert.Equal(0, await TrainingTestData.ReadPackBalanceAsync(
            _fixture.Factory, tenant.TrainerId, packId, Token));
    }

    [Fact]
    public async Task Archive_CancelsThePendingInvitation_AndReactivatingDoesNotBringItBack()
    {
        var trainerId = (await TrainerTenantSeeder.SeedTrainerAsync(_fixture.Factory, "archive-invite", Token)).TrainerId;
        var email = $"convidado-{Guid.NewGuid():N}@archive.test";
        var clientId = await TrainerTenantSeeder.SeedClientAsync(
            _fixture.Factory, trainerId, "Cliente convidado", Token, contactEmail: email);
        var trainer = TrainerClient(trainerId);
        var invite = await ApiJsonPayload.PostAsync(
            trainer, "/api/v1/auth/invite-client", new InviteClientRequest(clientId, email), Token);
        var before = await InvitationStateAsync(trainer, clientId);

        await trainer.PostAsync($"/api/v1/clients/{clientId}/archive", null, Token);
        await trainer.PostAsync($"/api/v1/clients/{clientId}/reactivate", null, Token);

        Assert.True(invite.IsSuccessStatusCode, await invite.Content.ReadAsStringAsync(Token));
        Assert.Equal("pending", before);
        Assert.Equal("none", await InvitationStateAsync(trainer, clientId));
        Assert.Equal(0, await CountAsync(db => db.InviteTokens.IgnoreQueryFilters()
            .CountAsync(token => token.ClientId == clientId, Token)));
    }

    [Fact]
    public async Task Archive_EndsTheClientsOpenSessions_AndOnlyTheirs()
    {
        var (trainerId, clientUserId, clientId) = await PortalTestData.SeedNewClientAsync(_fixture.Factory, Token);
        var (_, otherUserId, _) = await PortalTestData.SeedNewClientAsync(_fixture.Factory, Token, trainerId);
        await SeedRefreshTokenAsync(clientUserId);
        await SeedRefreshTokenAsync(otherUserId);
        var portal = PortalClient(trainerId, clientUserId);
        var before = await portal.GetAsync("/api/v1/portal/home", Token);

        await TrainerClient(trainerId).PostAsync($"/api/v1/clients/{clientId}/archive", null, Token);
        var after = await portal.GetAsync("/api/v1/portal/home", Token);
        var other = await PortalClient(trainerId, otherUserId).GetAsync("/api/v1/portal/home", Token);

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        Assert.Equal("authentication_session_ended", (await ReadJsonAsync(after)).GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
        Assert.Equal(0, await CountAsync(db => db.RefreshTokens
            .CountAsync(token => token.UserId == clientUserId && token.RevokedAt == null, Token)));
        Assert.Equal(1, await CountAsync(db => db.RefreshTokens
            .CountAsync(token => token.UserId == otherUserId && token.RevokedAt == null, Token)));
    }

    [Fact]
    public async Task ActiveLists_HideTheArchivedClient_ButTheClientPageStillShowsIt()
    {
        var tenant = await TrainingTestData.SeedTenantAsync(_fixture.Factory, Token);
        var trainer = TrainerClient(tenant.TrainerId);
        var plan = await ApiJsonPayload.PostAsync(
            trainer, "/api/v1/training-plans", TrainingTestData.SimplePlan(tenant), Token);
        var planId = (await ReadJsonAsync(plan)).GetProperty("id").GetGuid();

        await trainer.PostAsync($"/api/v1/clients/{tenant.ClientId}/archive", null, Token);
        var general = await ReadJsonAsync(await trainer.GetAsync(
            "/api/v1/training-plans?activity=active&page_size=100", Token));
        var clientPage = await ReadJsonAsync(await trainer.GetAsync(
            $"/api/v1/training-plans?activity=active&client_id={tenant.ClientId}", Token));

        Assert.DoesNotContain(general.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == planId);
        Assert.Contains(clientPage.GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == planId);
    }

    private async Task SeedRefreshTokenAsync(Guid userId)
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
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
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        return await count(scope.ServiceProvider.GetRequiredService<PtManagerDbContext>());
    }

    private static async Task<string?> SessionStatusAsync(HttpClient trainer, Guid sessionId) =>
        (await ReadJsonAsync(await trainer.GetAsync($"/api/v1/sessions/{sessionId}", Token)))
            .GetProperty("status").GetString();

    private static async Task<string?> InvitationStateAsync(HttpClient trainer, Guid clientId) =>
        (await ReadJsonAsync(await trainer.GetAsync($"/api/v1/clients/{clientId}/invitation", Token)))
            .GetProperty("state").GetString();

    private HttpClient TrainerClient(Guid trainerId) =>
        _fixture.Factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueTrainer(trainerId));

    private HttpClient PortalClient(Guid trainerId, Guid clientUserId) =>
        _fixture.Factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueClient(clientUserId, trainerId));

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(ApiJsonPayload.Options, Token);
}
