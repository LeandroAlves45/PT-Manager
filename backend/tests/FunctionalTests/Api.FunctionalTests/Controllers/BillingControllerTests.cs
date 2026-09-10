using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.FunctionalTests.Support;

namespace Api.FunctionalTests.Controllers;

/// <summary>
/// Prova o contrato HTTP de Billing exposto no Sprint 5B.
/// </summary>
/// <remarks>
/// A configuração funcional mantém Stripe desativado. Assim, os testes validam
/// autenticação, parsing estrito e falhas sanitizadas sem chamadas externas.
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

    [Fact]
    public async Task Checkout_CallerControlledRedirect_IsRejectedAsUnknownJson()
    {
        var trainer = await SeedTrainerAsync("billing-redirect-rejected");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/billing/checkout");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        request.Content = JsonContent.Create(new { tier = "PRO", success_url = "https://evil.example" });

        var response = await TrainerClient(trainer.TrainerId).SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CustomerPortal_WithoutIdempotencyKey_ReturnsBadRequest()
    {
        var trainer = await SeedTrainerAsync("billing-portal-key");

        var response = await TrainerClient(trainer.TrainerId).PostAsync(
            "/api/v1/billing/customer-portal",
            content: null,
            Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CustomerPortal_CallerControlledReturnUrl_IsRejectedAsUnknownJson()
    {
        var trainer = await SeedTrainerAsync("billing-portal-redirect");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/billing/customer-portal");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        request.Content = JsonContent.Create(new { return_url = "https://evil.example" });

        var response = await TrainerClient(trainer.TrainerId).SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_WithoutStripeSignature_ReturnsBadRequest()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/billing/webhook")
        {
            Content = JsonContent.Create(new { id = "evt_missing_signature" })
        };

        var response = await _factory.CreateOriginClient().SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Webhook_ExceedingConfiguredRawBodyLimit_ReturnsPayloadTooLarge()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/billing/webhook");
        request.Headers.Add("Stripe-Signature", "t=1,v1=invalid");
        request.Content = new StringContent(
            new string('x', 262_145),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _factory.CreateOriginClient().SendAsync(request, Token);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
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
