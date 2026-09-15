using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Api.FunctionalTests.Support;
using Application.Common.Abstractions;
using Domain.Entities.Identity;
using Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Security;

/// <summary>
/// Prova ponta a ponta (HTTP real, hasher de produção e PostgreSQL) que o lockout
/// de conta é aplicado.
/// </summary>
/// <remarks>
/// <para>
/// PTM-SEC-18: o <c>UserManager.AccessFailedAsync</c> define o <c>LockoutEnd</c> e
/// chama logo a seguir o reset do contador. Quando esse reset também limpava o
/// <c>LockoutEnd</c>, a conta nunca ficava bloqueada. O teste unitário existente usa
/// um store simulado e não o apanhava.
/// </para>
/// <para>
/// PTM-SEC-04: o change-password verificava a password atual sem contar falhas.
/// </para>
/// </remarks>
[Collection(ApiTestCollection.Name)]
public sealed class AccountLockoutTests : IAsyncLifetime
{
    private const string LoginRoute = "/api/v1/auth/login";
    private const string ChangePasswordRoute = "/api/v1/auth/change-password";
    private const string Password = "Functional-Password-1!";
    private const string WrongPassword = "Wrong-Password-9!";

    /// <summary>Igual a <c>Lockout.MaxFailedAccessAttempts</c> da configuração de Identity.</summary>
    private const int MaxFailedAccessAttempts = 5;

    private readonly PostgresApiFixture _database;
    private ApiWebApplicationFactory _factory = null!;

    public AccountLockoutTests(PostgresApiFixture database) => _database = database;

    public ValueTask InitializeAsync()
    {
        // Factory por teste: os rate limiters vivem no host e não podem ser
        // partilhados entre cenários que contam pedidos.
        _factory = new ApiWebApplicationFactory(_database.ConnectionString);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>Controlo: sem o controlo, a asserção 401 dos restantes testes não prova nada.</summary>
    [Fact]
    public async Task Login_WithCorrectPassword_Succeeds()
    {
        var email = await SeedTrainerWithPasswordAsync("lockout-control");
        var client = _factory.CreateOriginClient();

        var response = await LoginAsync(client, email, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_AfterFiveWrongPasswords_LocksAccountAndRejectsCorrectPassword()
    {
        var email = await SeedTrainerWithPasswordAsync("lockout-login");
        var client = _factory.CreateOriginClient();

        for (var attempt = 0; attempt < MaxFailedAccessAttempts; attempt++)
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                (await LoginAsync(client, email, WrongPassword)).StatusCode);

        var user = await ReadUserAsync(email);
        Assert.True(user.LockoutEnd > DateTime.UtcNow);
        Assert.Equal(0, user.AccessFailedCount);

        var correct = await LoginAsync(client, email, Password);

        Assert.Equal(HttpStatusCode.Unauthorized, correct.StatusCode);
        Assert.Equal("authentication_invalid_credentials", await ReadTitleAsync(correct));
    }

    [Fact]
    public async Task ChangePassword_AfterFiveWrongCurrentPasswords_LocksAccountAndRejectsLogin()
    {
        var email = await SeedTrainerWithPasswordAsync("lockout-change");
        var user = await ReadUserAsync(email);
        var authenticated = _factory.CreateOriginClient();
        authenticated.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtFactory.IssueTrainer(user.Id));

        // O rate limit de change-password é 5 por 15 minutos: exatamente o limite de lockout.
        for (var attempt = 0; attempt < MaxFailedAccessAttempts; attempt++)
        {
            var rejected = await ChangePasswordAsync(authenticated, WrongPassword);
            Assert.False(rejected.IsSuccessStatusCode);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        }

        var locked = await ReadUserAsync(email);
        Assert.True(locked.LockoutEnd > DateTime.UtcNow);

        var login = await LoginAsync(_factory.CreateOriginClient(), email, Password);

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_PersistsTheFailedAttempt()
    {
        var email = await SeedTrainerWithPasswordAsync("lockout-change-count");
        var user = await ReadUserAsync(email);
        var authenticated = _factory.CreateOriginClient();
        authenticated.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestJwtFactory.IssueTrainer(user.Id));

        var response = await ChangePasswordAsync(authenticated, WrongPassword);

        // Sem commit explícito, a transação do store faria rollback do contador.
        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(1, (await ReadUserAsync(email)).AccessFailedCount);
    }

    private Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync(
            LoginRoute,
            new { email, password },
            TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> ChangePasswordAsync(
        HttpClient client,
        string currentPassword) =>
        client.PostAsync(
            ChangePasswordRoute,
            JsonContent.Create(
                new
                {
                    current_password = currentPassword,
                    new_password = "Functional-Password-2!",
                    confirm_new_password = "Functional-Password-2!"
                },
                options: ApiJsonPayload.Options),
            TestContext.Current.CancellationToken);

    /// <summary>Semeia um trainer com a password real, gerada pelo hasher de produção.</summary>
    private async Task<string> SeedTrainerWithPasswordAsync(string discriminator)
    {
        var seeded = await TrainerTenantSeeder.SeedTrainerAsync(
            _factory, discriminator, TestContext.Current.CancellationToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider
            .GetRequiredService<ITenantContextInitializer>()
            .Establish(seeded.TrainerId, seeded.TrainerId, "trainer", TenantOrigin.System, false);
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        var user = await context.Users.SingleAsync(
            candidate => candidate.Id == seeded.TrainerId,
            TestContext.Current.CancellationToken);
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        user.SetPasswordHash(hasher.HashPassword(user, Password), TrainerTenantSeeder.SeedInstant);
        user.ConfirmEmail(TrainerTenantSeeder.SeedInstant);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return seeded.Email;
    }

    private async Task<User> ReadUserAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PtManagerDbContext>();
        return await context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(user => user.Email == email, TestContext.Current.CancellationToken);
    }

    private static async Task<string?> ReadTitleAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.GetProperty("title").GetString();
    }
}
