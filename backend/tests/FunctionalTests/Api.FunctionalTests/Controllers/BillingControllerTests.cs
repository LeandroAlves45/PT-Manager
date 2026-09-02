using System.Net;
using System.Text.Json;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Prova o único endpoint de Billing exposto na Fase 4: a leitura da subscrição.
/// </summary>
/// <remarks>
/// Checkout, portal Stripe e webhooks pertencem ao Sprint 5 e não estão expostos.
/// Um teste confirma que essas rotas continuam ausentes, para que a decisão de as
/// adiar seja verificável e não apenas documentada.
/// </remarks>
[Collection(ApiTestCollection.Name)]
public sealed class BillingControllerTests
{
    private readonly ApiWebApplicationFactory _factory;

    public BillingControllerTests(PostgresApiFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _factory = fixture.Factory;
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetSubscription_WithTrainerToken_ReturnsTheSeededSubscription()
    {
        var trainer = await SeedTrainerAsync("billing-happy");

        var response = await TrainerClient(trainer.TrainerId)
            .GetAsync("/api/v1/billing/subscription", Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await ReadJsonAsync(response);
        // Status e tier são os valores canónicos do domínio (maiúsculas), transportados
        // como texto opaco: a política snake_case do serializador aplica-se aos nomes
        // das propriedades, nunca ao conteúdo de um value object.
        Assert.Equal("ACTIVE", body.GetProperty("status").GetString());
        Assert.Equal("FREE", body.GetProperty("tier").GetString());
        Assert.Equal(5, body.GetProperty("client_limit").GetInt32());
        Assert.Equal(0, body.GetProperty("current_client_count").GetInt32());
    }

    [Fact]
    public async Task GetSubscription_ReflectsTheClientCountOfTheAuthenticatedTenant()
    {
        var trainer = await SeedTrainerAsync("billing-count");
        await TrainerTenantSeeder.SeedClientAsync(
            _factory,
            trainer.TrainerId,
            "Conta Um",
            Token);

        var body = await ReadJsonAsync(
            await TrainerClient(trainer.TrainerId)
                .GetAsync("/api/v1/billing/subscription", Token));

        Assert.Equal(1, body.GetProperty("current_client_count").GetInt32());
    }

    [Fact]
    public async Task GetSubscription_IsScopedToTheAuthenticatedTenant()
    {
        var withClient = await SeedTrainerAsync("billing-tenant-one");
        var withoutClient = await SeedTrainerAsync("billing-tenant-two");
        await TrainerTenantSeeder.SeedClientAsync(
            _factory,
            withClient.TrainerId,
            "Apenas Do Primeiro",
            Token);

        var body = await ReadJsonAsync(
            await TrainerClient(withoutClient.TrainerId)
                .GetAsync("/api/v1/billing/subscription", Token));

        // O contador do vizinho não pode contaminar a leitura deste tenant.
        Assert.Equal(0, body.GetProperty("current_client_count").GetInt32());
    }

    [Fact]
    public async Task GetSubscription_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.CreateOriginClient()
            .GetAsync("/api/v1/billing/subscription", Token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetSubscription_WithClientRole_ReturnsForbidden()
    {
        var caller = _factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueClient(Guid.NewGuid(), Guid.NewGuid()));

        var response = await caller.GetAsync("/api/v1/billing/subscription", Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSubscription_WithSuperuserRole_ReturnsForbidden()
    {
        var caller = _factory.CreateOriginClient()
            .WithBearer(TestJwtFactory.IssueSuperuser(Guid.NewGuid()));

        var response = await caller.GetAsync("/api/v1/billing/subscription", Token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSubscription_ForATrainerWithoutSubscription_ReturnsNotFound()
    {
        // Nenhum seed: o tenant do token não tem linha de subscrição.
        var response = await TrainerClient(Guid.NewGuid())
            .GetAsync("/api/v1/billing/subscription", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/billing/checkout")]
    [InlineData("/api/v1/billing/customer-portal")]
    [InlineData("/api/v1/billing/webhook")]
    public async Task DeferredBillingRoutes_AreNotExposedInThisPhase(string route)
    {
        // O último segmento identifica a rota de forma estável; string.GetHashCode() é
        // aleatorizado por processo em .NET e não serve como discriminador legível.
        var trainer = await SeedTrainerAsync(
            $"billing-absent-{route[(route.LastIndexOf('/') + 1)..]}");

        var response = await TrainerClient(trainer.TrainerId).PostAsync(
            route,
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task<SeededTrainer> SeedTrainerAsync(string discriminator) =>
        TrainerTenantSeeder.SeedTrainerAsync(_factory, discriminator, Token);

    private HttpClient TrainerClient(Guid trainerId) =>
        _factory.CreateOriginClient().WithBearer(TestJwtFactory.IssueTrainer(trainerId));

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync(Token);
        return JsonDocument.Parse(payload).RootElement.Clone();
    }
}
