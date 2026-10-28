using System.Net;
using System.Text.Json;
using Api.FunctionalTests.Support;
using Application.Common.Abstractions;
using Domain.Entities.Assessments;
using Domain.Entities.Clients;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// O cliente elimina a própria conta: deixa de entrar, os dados que o identificam
/// desaparecem e o histórico fica no tenant do personal trainer sem identificação.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class ClientPortalAccountDeletionTests : IAsyncLifetime
{
    private const string DeletionRoute = "/api/v1/portal/my-account/deletion";
    private const string RegisterRoute = "/api/v1/auth/accept-invite/register";
    private const string LoginRoute = "/api/v1/auth/login";
    private const string MedicalConditions = "Asma e hipertensão";
    private const string CheckInNotes = "Dormi mal por causa do trabalho";

    private readonly PostgresApiFixture _database;
    private ApiWebApplicationFactory _factory = null!;

    public ClientPortalAccountDeletionTests(PostgresApiFixture database) => _database = database;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

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
    public async Task Delete_AnonymizesTheClientAndKeepsTheHistoryWithTheTrainer()
    {
        var account = await RegisterClientWithHistoryAsync("delete-ok");
        var countBefore = await ClientCountAsync(account.TrainerId);

        var response = await DeleteAsync(account.AccessToken, new { password = AuthTestData.Password, confirmation = "ELIMINAR" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();

        var user = await context.Users.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(candidate => candidate.Id == account.UserId, Token);
        Assert.True(user.IsDeleted);
        Assert.Null(user.PasswordHash);
        Assert.Null(user.FullName);
        Assert.Equal($"deleted-{account.UserId:N}@deleted.invalid", user.Email);

        var client = await context.Clients.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(candidate => candidate.Id == account.ClientId, Token);
        Assert.Equal(Client.AnonymizedName, client.Name);
        Assert.Null(client.ContactEmail);
        Assert.Null(client.Notes);
        Assert.Null(client.EmergencyContactName);
        Assert.False(client.IsActive);
        Assert.False(client.IsDeleted);
        Assert.Equal(1, client.BirthDate.Value.Day);

        var assessment = await context.InitialAssessments.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(candidate => candidate.ClientId == account.ClientId, Token);
        Assert.Null(assessment.MedicalConditions);
        Assert.Null(assessment.Profession);
        Assert.Equal("Removido", assessment.Goals);
        Assert.Equal(72m, assessment.WeightKg);

        var checkIn = await context.CheckIns.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(candidate => candidate.ClientId == account.ClientId, Token);
        Assert.Null(checkIn.Notes);

        Assert.False(await context.RefreshTokens.AsNoTracking()
            .AnyAsync(token => token.UserId == account.UserId && token.RevokedAt == null, Token));
        Assert.Equal(countBefore - 1, await ClientCountAsync(account.TrainerId));
    }

    [Fact]
    public async Task Delete_TheAccountCanNoLongerLogInAndTheEmailCanBeInvitedAgain()
    {
        var account = await RegisterClientWithHistoryAsync("delete-login");
        await DeleteAsync(account.AccessToken, new { password = AuthTestData.Password, confirmation = "ELIMINAR" });

        var login = await ApiJsonPayload.PostAsync(
            _factory.CreateOriginClient(),
            LoginRoute,
            new { email = account.Email, password = AuthTestData.Password },
            Token);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        var again = await AuthTestData.SeedInvitationAsync(_factory, "delete-again", email: account.Email);
        var register = await RegisterAsync(_factory.CreateOriginClient(), again.RawToken);
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
    }

    [Fact]
    public async Task Delete_WrongPassword_Is400OnThePasswordFieldAndChangesNothing()
    {
        var account = await RegisterClientWithHistoryAsync("delete-wrong");

        var response = await DeleteAsync(account.AccessToken, new { password = "Wrong-Password-1!", confirmation = "ELIMINAR" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("portal_account_deletion_password_invalid", await FirstErrorCodeAsync(response));
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var client = await context.Clients.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(candidate => candidate.Id == account.ClientId, Token);
        Assert.True(client.IsActive);
        Assert.NotEqual(Client.AnonymizedName, client.Name);
        Assert.Equal(MedicalConditions, await context.InitialAssessments.IgnoreQueryFilters().AsNoTracking()
            .Where(candidate => candidate.ClientId == account.ClientId)
            .Select(candidate => candidate.MedicalConditions)
            .SingleAsync(Token));
    }

    [Fact]
    public async Task Delete_WithoutTheConfirmationWord_Is400()
    {
        var account = await RegisterClientWithHistoryAsync("delete-confirm");

        var response = await DeleteAsync(account.AccessToken, new { password = AuthTestData.Password, confirmation = "sim" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("portal_account_deletion_confirmation_required", await FirstErrorCodeAsync(response));
    }

    [Fact]
    public async Task Delete_LeavesTheTrainersOtherClientsUntouched()
    {
        var account = await RegisterClientWithHistoryAsync("delete-other");
        var otherClientId = await TrainerTenantSeeder.SeedClientAsync(
            _factory, account.TrainerId, "Outro Cliente", Token);

        await DeleteAsync(account.AccessToken, new { password = AuthTestData.Password, confirmation = "ELIMINAR" });

        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var other = await context.Clients.IgnoreQueryFilters().AsNoTracking()
            .SingleAsync(candidate => candidate.Id == otherClientId, Token);
        Assert.Equal(("Outro Cliente", true), (other.Name, other.IsActive));
    }

    [Fact]
    public async Task Delete_ByATrainer_IsForbidden()
    {
        var trainer = await TrainerTenantSeeder.SeedTrainerAsync(_factory, "delete-trainer", Token);
        var client = _factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueTrainer(trainer.TrainerId));

        var response = await ApiJsonPayload.PostAsync(
            client, DeletionRoute, new { password = "x", confirmation = "ELIMINAR" }, Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<RegisteredClient> RegisterClientWithHistoryAsync(string prefix)
    {
        var invitation = await AuthTestData.SeedInvitationAsync(_factory, prefix);
        var response = await RegisterAsync(_factory.CreateOriginClient(), invitation.RawToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        var userId = body.RootElement.GetProperty("user_id").GetGuid();
        var accessToken = body.RootElement.GetProperty("access_token").GetString()!;

        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider
            .GetRequiredService<ITenantContextInitializer>()
            .Establish(invitation.TrainerId, invitation.TrainerId, "trainer", TenantOrigin.System, false);
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var checkIn = new CheckIn(
            invitation.TrainerId,
            invitation.ClientId,
            new DateOnly(2026, 9, 1),
            null,
            TrainerTenantSeeder.SeedInstant);
        context.CheckIns.Add(checkIn);
        await context.SaveChangesAsync(Token);

        // A ficha semeada já tem avaliação inicial: recebe as respostas do cliente.
        await context.InitialAssessments.IgnoreQueryFilters()
            .Where(candidate => candidate.ClientId == invitation.ClientId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.MedicalConditions, MedicalConditions)
                    .SetProperty(candidate => candidate.Profession, "Engenheira")
                    .SetProperty(candidate => candidate.WeightKg, 72m),
                Token);
        await context.CheckIns.IgnoreQueryFilters()
            .Where(candidate => candidate.Id == checkIn.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(candidate => candidate.Notes, CheckInNotes), Token);
        await context.Clients.IgnoreQueryFilters()
            .Where(candidate => candidate.Id == invitation.ClientId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.Notes, "Notas do personal trainer")
                    .SetProperty(candidate => candidate.EmergencyContactName, "Rui"),
                Token);

        return new RegisteredClient(
            invitation.TrainerId, invitation.ClientId, userId, invitation.Email, accessToken);
    }

    private Task<HttpResponseMessage> DeleteAsync(string accessToken, object body) =>
        ApiJsonPayload.PostAsync(
            _factory.CreateOriginClient().WithBearer(accessToken), DeletionRoute, body, Token);

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string token) =>
        ApiJsonPayload.PostAsync(
            client,
            RegisterRoute,
            new { token, password = AuthTestData.Password, confirmPassword = AuthTestData.Password },
            Token);

    private async Task<int> ClientCountAsync(Guid trainerId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        return await context.TrainerSubscriptions.IgnoreQueryFilters().AsNoTracking()
            .Where(subscription => subscription.TrainerId == trainerId)
            .Select(subscription => subscription.CurrentClientCount)
            .SingleAsync(Token);
    }

    private static async Task<string?> FirstErrorCodeAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        return body.RootElement.GetProperty("errors")[0].GetProperty("code").GetString();
    }

    private sealed record RegisteredClient(
        Guid TrainerId, Guid ClientId, Guid UserId, string Email, string AccessToken);
}
