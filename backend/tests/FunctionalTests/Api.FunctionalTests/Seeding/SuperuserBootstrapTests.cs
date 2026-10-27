using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.FunctionalTests.Support;
using Infrastructure.Seeding;
using Microsoft.Extensions.DependencyInjection;

namespace Api.FunctionalTests.Seeding;

/// <summary>
/// Prova a criação do primeiro superuser em produção contra bases PostgreSQL descartáveis.
/// </summary>
/// <remarks>
/// Corre em <c>Testing</c> de propósito: o seed de desenvolvimento nem é registado, por isso
/// só a criação por <c>SuperuserBootstrap</c> pode ter posto o utilizador na base.
/// </remarks>
public sealed class SuperuserBootstrapTests : IClassFixture<ScratchPostgresFixture>
{
    private const string Email = "owner@bootstrap.test";
    private const string Password = "Bootstrap-Password-1!";

    private readonly ScratchPostgresFixture _postgres;

    public SuperuserBootstrapTests(ScratchPostgresFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Disabled_CreatesNoUser()
    {
        var connectionString = await CreateMigratedDatabaseAsync();
        using var factory = CreateHost(connectionString, enabled: false, password: Password, email: Email);
        _ = factory.Services;

        Assert.Equal(0, await CountUsersAsync(connectionString));
    }

    [Fact]
    public async Task Enabled_CreatesAConfirmedSuperuserThatCanLogIn()
    {
        var connectionString = await CreateMigratedDatabaseAsync();
        using var factory = CreateHost(connectionString, enabled: true, password: Password, email: Email);
        _ = factory.Services;

        Assert.Equal(
            1,
            await ScratchPostgresFixture.ScalarAsync(
                connectionString,
                "SELECT count(*) FROM users WHERE role = 'superuser' AND email_confirmed",
                Cancellation));

        var client = factory.CreateOriginClient();
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email = Email, password = Password }, Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        Assert.Equal("superuser", body.RootElement.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Enabled_TwiceDoesNotCreateASecondSuperuser()
    {
        var connectionString = await CreateMigratedDatabaseAsync();

        using (var first = CreateHost(connectionString, enabled: true, password: Password, email: Email))
            _ = first.Services;

        using var second = CreateHost(
            connectionString, enabled: true, password: "Another-Password-2!", email: "other@bootstrap.test");
        _ = second.Services;

        Assert.Equal(1, await CountUsersAsync(connectionString));
    }

    [Fact]
    public async Task Enabled_RefusesAnEmailThatBelongsToANonSuperuser()
    {
        var connectionString = await CreateMigratedDatabaseAsync();
        using (var seeded = CreateHost(connectionString, enabled: true, password: Password, email: Email))
            _ = seeded.Services;
        await ScratchPostgresFixture.ExecuteAsync(
            connectionString, "UPDATE users SET role = 'trainer';", Cancellation);

        using var again = CreateHost(connectionString, enabled: true, password: Password, email: Email);

        // O arranque propaga a falha: a API não deve parecer pronta sem o superuser pedido.
        Assert.ThrowsAny<Exception>(() => _ = again.Services);
    }

    [Theory]
    [InlineData("", "Bootstrap-Password-1!")]
    [InlineData("owner@bootstrap.test", "short-pass")]
    public void Options_WhenEnabledWithoutEmailOrWithAShortPassword_AreInvalid(string email, string password)
    {
        var options = new SuperuserBootstrapOptions { Enabled = true, Email = email, Password = password };

        Assert.False(options.IsValid());
    }

    [Fact]
    public void Options_WhenDisabled_AreAlwaysValid() =>
        Assert.True(new SuperuserBootstrapOptions { Enabled = false }.IsValid());

    private async Task<string> CreateMigratedDatabaseAsync()
    {
        var connectionString = await _postgres.CreateDatabaseAsync(Cancellation);
        await ScratchPostgresFixture.MigrateAsync(connectionString, Cancellation);
        return connectionString;
    }

    private static ApiWebApplicationFactory CreateHost(
        string connectionString, bool enabled, string password, string email) =>
        new(connectionString)
        {
            AdditionalSettings = new Dictionary<string, string>
            {
                ["SuperuserBootstrap:Enabled"] = enabled ? "true" : "false",
                ["SuperuserBootstrap:Email"] = email,
                ["SuperuserBootstrap:Password"] = password
            }
        };

    private static Task<long> CountUsersAsync(string connectionString) =>
        ScratchPostgresFixture.ScalarAsync(connectionString, "SELECT count(*) FROM users", Cancellation);
}
