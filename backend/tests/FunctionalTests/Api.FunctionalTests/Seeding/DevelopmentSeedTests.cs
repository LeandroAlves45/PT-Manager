using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Contracts.Assessments;
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
    private const string SecondClientEmail = "cliente2@seed.test";

    /// <summary>Contagens exatas de uma base vazia depois do seed (doc 13).</summary>
    private static readonly IReadOnlyDictionary<string, long> ExpectedCounts =
        new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["users"] = 5,
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
            ["checkins"] = 3,
            ["client_supplement_assignments"] = 1,
            ["client_supplement_intakes"] = 1,
            ["administrative_audit_entries"] = 5
        };

    /// <summary>Resposta mínima válida a um check-in, igual à dos testes do portal.</summary>
    private static readonly CheckInAnswerRequest Answer =
        new(71.5m, 17m, "All good", null, null, 80, 75);

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
    public async Task Enabled_SeedsCompleteEnvironment_AndAllFiveAccountsLogIn()
    {
        var connectionString = await CreateMigratedDatabaseAsync();
        using var factory = CreateDevelopmentHost(connectionString, enabled: true);

        Assert.True(ReadEffectiveOptions(factory).Enabled);
        Assert.Equal(ExpectedCounts, await ReadCountsAsync(connectionString));
        Assert.Equal(5, await ScratchPostgresFixture.ScalarAsync(
            connectionString,
            "SELECT count(*) FROM users WHERE email_confirmed",
            Cancellation));

        // Login real pelo endpoint: prova hash, email confirmado e role de uma só vez.
        var client = factory.CreateOriginClient();
        Assert.Equal("superuser", await LoginAndReadRoleAsync(client, SuperuserEmail));
        Assert.Equal("trainer", await LoginAndReadRoleAsync(client, TrainerEmail));
        Assert.Equal("client", await LoginAndReadRoleAsync(client, ClientEmail));
        Assert.Equal("trainer", await LoginAndReadRoleAsync(client, SecondTrainerEmail));
        Assert.Equal("client", await LoginAndReadRoleAsync(client, SecondClientEmail));
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

    /// <summary>
    /// Cada cliente semeado vê o portal do seu trainer: o João tem a marca própria e um
    /// check-in por responder hoje; a Marta vê a marca por omissão e não tem check-in.
    /// </summary>
    [Fact]
    public async Task SeededClients_SeeOnlyTheirOwnPortal()
    {
        var connectionString = await CreateMigratedDatabaseAsync();
        using var factory = CreateDevelopmentHost(connectionString, enabled: true);
        var client = factory.CreateOriginClient();

        var joao = await LoginAsync(client, ClientEmail);
        using (var branding = await GetJsonAsync(client, joao, "/api/v1/portal/branding", HttpStatusCode.OK))
        {
            Assert.Equal("Salgado Performance", branding.RootElement.GetProperty("app_name").GetString());
            Assert.Equal("#E8642A", branding.RootElement.GetProperty("primary_color").GetString());
            Assert.Equal("#1F1A17", branding.RootElement.GetProperty("body_color").GetString());
        }

        using (var home = await GetJsonAsync(client, joao, "/api/v1/portal/home", HttpStatusCode.OK))
        {
            var nextCheckIn = home.RootElement.GetProperty("next_check_in");
            Assert.True(nextCheckIn.GetProperty("is_today").GetBoolean());
            Assert.Equal(
                home.RootElement.GetProperty("local_date").GetString(),
                nextCheckIn.GetProperty("check_in_date").GetString());
        }

        (await GetJsonAsync(client, joao, "/api/v1/portal/my-check-ins/due", HttpStatusCode.OK)).Dispose();

        var marta = await LoginAsync(client, SecondClientEmail);
        using (var branding = await GetJsonAsync(client, marta, "/api/v1/portal/branding", HttpStatusCode.OK))
        {
            Assert.Equal("PT Manager", branding.RootElement.GetProperty("app_name").GetString());
            Assert.Equal(JsonValueKind.Null, branding.RootElement.GetProperty("primary_color").ValueKind);
        }

        using (var profile = await GetJsonAsync(client, marta, "/api/v1/portal/my-profile", HttpStatusCode.OK))
            Assert.Equal("Marta Figueiredo", profile.RootElement.GetProperty("name").GetString());

        (await GetJsonAsync(client, marta, "/api/v1/portal/my-check-ins/due", HttpStatusCode.NotFound)).Dispose();
    }

    /// <summary>
    /// O check-in de hoje do João existe para ser respondido no portal: com o dia errado,
    /// o pedido seria recusado com <c>check_in_wrong_day</c>. Depois da resposta deixa de
    /// haver check-in por responder.
    /// </summary>
    [Fact]
    public async Task SeededDueCheckIn_CanBeAnsweredByItsClient()
    {
        var connectionString = await CreateMigratedDatabaseAsync();
        using var factory = CreateDevelopmentHost(connectionString, enabled: true);
        var client = factory.CreateOriginClient();

        var joao = await LoginAsync(client, ClientEmail);
        var checkInId = await ReadDueCheckInIdAsync(client, joao);

        using (var answered = await PostJsonAsync(
            client, joao, $"/api/v1/portal/check-ins/{checkInId}/respond", Answer, HttpStatusCode.OK))
        {
            Assert.Equal(checkInId, answered.RootElement.GetProperty("id").GetGuid());
            Assert.Equal(JsonValueKind.String, answered.RootElement.GetProperty("responded_at").ValueKind);
        }

        (await GetJsonAsync(client, joao, "/api/v1/portal/my-check-ins/due", HttpStatusCode.NotFound)).Dispose();
    }

    /// <summary>
    /// A conta da Marta pertence a outro tenant: conhecer o id do check-in do João não lhe
    /// dá acesso, e o check-in continua por responder.
    /// </summary>
    [Fact]
    public async Task SeededClientOfAnotherTrainer_CannotAnswerForeignCheckIn()
    {
        var connectionString = await CreateMigratedDatabaseAsync();
        using var factory = CreateDevelopmentHost(connectionString, enabled: true);
        var client = factory.CreateOriginClient();

        var joao = await LoginAsync(client, ClientEmail);
        var checkInId = await ReadDueCheckInIdAsync(client, joao);
        var marta = await LoginAsync(client, SecondClientEmail);

        (await PostJsonAsync(
            client, marta, $"/api/v1/portal/check-ins/{checkInId}/respond", Answer, HttpStatusCode.NotFound))
            .Dispose();

        Assert.Equal(checkInId, await ReadDueCheckInIdAsync(client, joao));
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

    /// <summary>
    /// Os elementos da 6F (conta da Marta e check-in de hoje do João) fazem parte do seed
    /// completo: uma base semeada antes da 6F não arranca sem reset, com o diagnóstico certo.
    /// </summary>
    [Theory]
    [InlineData(
        "UPDATE users SET email_confirmed = false WHERE normalized_email = 'CLIENTE2@SEED.TEST'",
        "confirmed client CLIENTE2@SEED.TEST")]
    [InlineData(
        "DELETE FROM checkins WHERE id = (SELECT id FROM checkins WHERE responded_at IS NULL LIMIT 1)",
        "check-ins")]
    public async Task PartialSeed_WithoutSprint6FElements_FailsStartupWithDiagnostic(
        string breakSeed,
        string expectedMissing)
    {
        var connectionString = await CreateMigratedDatabaseAsync();
        using (var seeded = CreateDevelopmentHost(connectionString, enabled: true))
            _ = seeded.Services;

        await ScratchPostgresFixture.ExecuteAsync(connectionString, breakSeed, Cancellation);

        using var restarted = CreateDevelopmentHost(connectionString, enabled: true);
        var exception = Assert.ThrowsAny<Exception>(() => restarted.Services);

        var message = FlattenMessages(exception);
        Assert.Contains("Development seed is partial", message, StringComparison.Ordinal);
        Assert.Contains(expectedMissing, message, StringComparison.Ordinal);
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
        ["DevelopmentSeed:SecondTrainerEmail"] = SecondTrainerEmail,
        ["DevelopmentSeed:SecondClientEmail"] = SecondClientEmail
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

    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        using var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { email, password = Password }, Cancellation);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var session = JsonDocument.Parse(await login.Content.ReadAsStringAsync(Cancellation));
        return session.RootElement.GetProperty("access_token").GetString()!;
    }

    private static async Task<JsonDocument> GetJsonAsync(
        HttpClient client,
        string accessToken,
        string path,
        HttpStatusCode expected)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new("Bearer", accessToken);
        using var response = await client.SendAsync(request, Cancellation);
        var body = await response.Content.ReadAsStringAsync(Cancellation);
        Assert.True(response.StatusCode == expected, body);
        return JsonDocument.Parse(body);
    }

    private static async Task<JsonDocument> PostJsonAsync<TRequest>(
        HttpClient client,
        string accessToken,
        string path,
        TRequest payload,
        HttpStatusCode expected)
    {
        // Mesma política snake_case da API: com a omissão do PostAsJsonAsync o corpo chega vazio.
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload, options: ApiJsonPayload.Options)
        };
        request.Headers.Authorization = new("Bearer", accessToken);
        using var response = await client.SendAsync(request, Cancellation);
        var body = await response.Content.ReadAsStringAsync(Cancellation);
        Assert.True(response.StatusCode == expected, body);
        return JsonDocument.Parse(body);
    }

    private static async Task<Guid> ReadDueCheckInIdAsync(HttpClient client, string accessToken)
    {
        using var due = await GetJsonAsync(
            client, accessToken, "/api/v1/portal/my-check-ins/due", HttpStatusCode.OK);
        return due.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<string[]> ListClientNamesAsync(HttpClient client, string email)
    {
        var accessToken = await LoginAsync(client, email);

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
