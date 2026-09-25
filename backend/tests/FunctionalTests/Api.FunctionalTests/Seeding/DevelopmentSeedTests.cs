using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.FunctionalTests.Support;
using Infrastructure.Seeding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.FunctionalTests.Seeding;

/// <summary>
/// Prova o seed de desenvolvimento contra bases PostgreSQL descartáveis (QG6C-SEED-001).
/// </summary>
/// <remarks>
/// <para>
/// Cada cenário cria uma base vazia, aplica as migrations explicitamente num host
/// <c>Testing</c> (onde o seed nem é registado) e só depois arranca o host
/// <c>Development</c> com o seed ligado. Nenhum teste toca a base de desenvolvimento.
/// </para>
/// <para>
/// As definições do seed são sempre explícitas: os User Secrets do programador também
/// são lidos em Development, e o teste afirma o valor efetivo para falhar alto se a
/// precedência de configuração mudar.
/// </para>
/// </remarks>
public sealed class DevelopmentSeedTests : IClassFixture<ScratchPostgresFixture>
{
    private const string Password = "Seed-Functional-Password-1!";
    private const string SuperuserEmail = "admin@seed.test";
    private const string TrainerEmail = "trainer@seed.test";
    private const string ClientEmail = "cliente@seed.test";
    private const string SecondTrainerEmail = "trainer2@seed.test";

    /// <summary>Contagens exatas de uma base vazia depois do seed (doc 13).</summary>
    private static readonly IReadOnlyDictionary<string, long> ExpectedCounts =
        new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["users"] = 4,
            ["clients"] = 3,
            ["initial_assessments"] = 1,
            ["trainer_subscriptions"] = 2,
            ["trainer_settings"] = 2,
            ["exercises"] = 3,
            ["foods"] = 3,
            ["supplements"] = 2,
            ["training_plans"] = 1,
            ["training_plan_days"] = 2,
            ["exercise_sets"] = 10,
            ["client_exercise_set_logs"] = 4,
            ["workout_completions"] = 1,
            ["meal_plans"] = 1,
            ["meal_plan_meal_items"] = 2,
            ["sessions"] = 2,
            ["pack_types"] = 1,
            ["client_session_packs"] = 1,
            ["checkins"] = 2,
            ["client_supplement_assignments"] = 1,
            ["client_supplement_intakes"] = 1,
            ["administrative_audit_entries"] = 5
        };

    private readonly ScratchPostgresFixture _postgres;

    public DevelopmentSeedTests(ScratchPostgresFixture postgres) => _postgres = postgres;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Disabled_StartsWithoutSeeding()
    {
        var connectionString = await CreateMigratedDatabaseAsync();
        using var factory = CreateDevelopmentHost(connectionString, enabled: false);

        Assert.False(ReadEffectiveOptions(factory).Enabled);
        Assert.Equal(0, await CountAsync(connectionString, "users"));
    }

    [Fact]
    public async Task Enabled_SeedsCompleteEnvironment_AndAllFourAccountsLogIn()
    {
        var connectionString = await CreateMigratedDatabaseAsync();
        using var factory = CreateDevelopmentHost(connectionString, enabled: true);

        Assert.True(ReadEffectiveOptions(factory).Enabled);
        Assert.Equal(ExpectedCounts, await ReadCountsAsync(connectionString));
        Assert.Equal(4, await ScratchPostgresFixture.ScalarAsync(
            connectionString,
            "SELECT count(*) FROM users WHERE email_confirmed",
            Cancellation));

        // Login real pelo endpoint: prova hash, email confirmado e role de uma só vez.
        var client = factory.CreateOriginClient();
        Assert.Equal("superuser", await LoginAndReadRoleAsync(client, SuperuserEmail));
        Assert.Equal("trainer", await LoginAndReadRoleAsync(client, TrainerEmail));
        Assert.Equal("client", await LoginAndReadRoleAsync(client, ClientEmail));
        Assert.Equal("trainer", await LoginAndReadRoleAsync(client, SecondTrainerEmail));
    }

    [Fact]
    public async Task SeededTrainers_SeeOnlyTheirOwnClients()
    {
        var connectionString = await CreateMigratedDatabaseAsync();
        using var factory = CreateDevelopmentHost(connectionString, enabled: true);
        var client = factory.CreateOriginClient();

        Assert.Equal(
            ["Ana Costa", "João Pereira"],
            await ListClientNamesAsync(client, TrainerEmail));
        Assert.Equal(
            ["Marta Figueiredo"],
            await ListClientNamesAsync(client, SecondTrainerEmail));
    }

    [Fact]
    public async Task SecondStart_IsIdempotent()
    {
        var connectionString = await CreateMigratedDatabaseAsync();
        using (var first = CreateDevelopmentHost(connectionString, enabled: true))
            _ = first.Services;

        using var second = CreateDevelopmentHost(connectionString, enabled: true);
        _ = second.Services;

        Assert.Equal(ExpectedCounts, await ReadCountsAsync(connectionString));
    }

    /// <summary>
    /// O superuser existir não prova que o seed terminou. Um agregado em falta tem de
    /// impedir o arranque com um diagnóstico, e nunca ser reparado em silêncio.
    /// </summary>
    [Fact]
    public async Task PartialSeed_FailsStartupWithDiagnostic()
    {
        var connectionString = await CreateMigratedDatabaseAsync();
        using (var seeded = CreateDevelopmentHost(connectionString, enabled: true))
            _ = seeded.Services;

        await ScratchPostgresFixture.ExecuteAsync(
            connectionString, "DELETE FROM workout_completions", Cancellation);

        using var restarted = CreateDevelopmentHost(connectionString, enabled: true);
        var exception = Assert.ThrowsAny<Exception>(() => restarted.Services);

        var message = FlattenMessages(exception);
        Assert.Contains("Development seed is partial", message, StringComparison.Ordinal);
        Assert.Contains("workout completion", message, StringComparison.Ordinal);
        Assert.Equal(0, await CountAsync(connectionString, "workout_completions"));
    }

    [Fact]
    public async Task EnabledWithoutPassword_FailsOptionsValidationBeforeWriting()
    {
        var connectionString = await CreateMigratedDatabaseAsync();
        using var factory = CreateDevelopmentHost(connectionString, enabled: true, password: string.Empty);

        var exception = Assert.ThrowsAny<OptionsValidationException>(() => factory.Services);

        Assert.Contains("DevelopmentSeed:Password", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, await CountAsync(connectionString, "users"));
    }

    [Fact]
    public async Task OutsideDevelopment_SeedIsNotRegisteredEvenWhenEnabled()
    {
        var connectionString = await CreateMigratedDatabaseAsync();
        using var factory = new ApiWebApplicationFactory(connectionString, "Testing")
        {
            AdditionalSettings = SeedSettings(enabled: true, Password)
        };

        Assert.Null(factory.Services.GetService<DevelopmentDataSeeder>());
        Assert.Equal(0, await CountAsync(connectionString, "users"));
    }

    private async Task<string> CreateMigratedDatabaseAsync()
    {
        var connectionString = await _postgres.CreateDatabaseAsync(Cancellation);
        await ScratchPostgresFixture.MigrateAsync(connectionString, Cancellation);
        return connectionString;
    }

    private static ApiWebApplicationFactory CreateDevelopmentHost(
        string connectionString,
        bool enabled,
        string password = Password) =>
        new(connectionString, "Development")
        {
            AdditionalSettings = SeedSettings(enabled, password)
        };

    private static Dictionary<string, string> SeedSettings(bool enabled, string password) => new()
    {
        ["DevelopmentSeed:Enabled"] = enabled ? "true" : "false",
        ["DevelopmentSeed:Password"] = password,
        ["DevelopmentSeed:SuperuserEmail"] = SuperuserEmail,
        ["DevelopmentSeed:TrainerEmail"] = TrainerEmail,
        ["DevelopmentSeed:ClientEmail"] = ClientEmail,
        ["DevelopmentSeed:SecondTrainerEmail"] = SecondTrainerEmail
    };

    private static DevelopmentSeedOptions ReadEffectiveOptions(ApiWebApplicationFactory factory) =>
        factory.Services.GetRequiredService<IOptions<DevelopmentSeedOptions>>().Value;

    private static async Task<string?> LoginAndReadRoleAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password = Password }, Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancellation));
        return body.RootElement.GetProperty("role").GetString();
    }

    private static async Task<string[]> ListClientNamesAsync(HttpClient client, string email)
    {
        using var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password = Password }, Cancellation);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var session = JsonDocument.Parse(await login.Content.ReadAsStringAsync(Cancellation));
        var accessToken = session.RootElement.GetProperty("access_token").GetString();

        using var request = new HttpRequestMessage(
            HttpMethod.Get, "/api/v1/clients?page_number=1&page_size=50");
        request.Headers.Authorization = new("Bearer", accessToken);
        using var response = await client.SendAsync(request, Cancellation);
        var body = await response.Content.ReadAsStringAsync(Cancellation);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var page = JsonDocument.Parse(body);
        return page.RootElement.GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("name").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static async Task<Dictionary<string, long>> ReadCountsAsync(string connectionString)
    {
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var table in ExpectedCounts.Keys)
            counts[table] = await CountAsync(connectionString, table);

        return counts;
    }

    /// <summary>Contagem por SQL direto: os nomes vêm apenas da lista fixa acima.</summary>
    private static Task<long> CountAsync(string connectionString, string table) =>
        ScratchPostgresFixture.ScalarAsync(
            connectionString, $"SELECT count(*) FROM {table}", Cancellation);

    private static string FlattenMessages(Exception exception)
    {
        var messages = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            messages.Add(current.Message);
            if (current is AggregateException aggregate)
                messages.AddRange(aggregate.InnerExceptions.Select(inner => inner.Message));
        }

        return string.Join(" | ", messages);
    }
}
