using Application.Features.Billing.Abstractions;
using Domain.ValueObjects;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Payments.Stripe;
using Infrastructure.Persistence.Billing;
using Microsoft.Extensions.Options;

namespace Infrastructure.IntegrationTests.Billing;

/// <summary>
/// Regressões da Fase 5B que a bateria do blueprint 15 não cobria. Cada teste falha
/// se o defeito correspondente for reintroduzido; a mutação foi confirmada antes de
/// o teste ser escrito.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class BillingRegressionTests(PostgresContainerFixture database)
{
    /// <summary>
    /// Um Checkout abandonado deixa o customer associado sem subscrição externa. Esse
    /// estado não pode ser lido como "já subscrito": o personal trainer continua no
    /// tier FREE com estado Active e tem de conseguir reiniciar o Checkout. Quando a
    /// guarda usava StripeCustomerId em vez de StripeSubscriptionId, a primeira
    /// tentativa falhada trancava o upgrade de forma permanente.
    /// </summary>
    [Fact]
    public async Task Reserve_CustomerLinkedWithoutSubscription_StillAllowsCheckout()
    {
        var token = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var trainer = await support.SeedTrainerAsync(
            "reserve-customer-only",
            customerId: $"cus_{Guid.NewGuid():N}",
            cancellationToken: token);
        await using var context = support.CreateRetryingTrainerContext(trainer.TrainerId);

        var result = await new BillingCheckoutStore(context).ReserveAsync(
            trainer.TrainerId,
            Guid.NewGuid(),
            SubscriptionTier.Starter,
            BillingTestSupport.Now,
            TimeSpan.FromMinutes(2),
            token);

        Assert.Equal(CheckoutReservationStatus.Acquired, result.Status);
        Assert.NotNull(result.ProviderCustomerId);
    }

    /// <summary>
    /// Com uma subscrição externa ativa o Checkout deixa de ser o caminho correto e a
    /// reserva encaminha para o Customer Portal. Este é o contraponto do teste anterior
    /// e impede que a correção da guarda passe a aceitar tudo.
    /// </summary>
    [Fact]
    public async Task Reserve_ActiveSubscriptionLinked_RoutesToCustomerPortal()
    {
        var token = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var trainer = await support.SeedTrainerAsync(
            "reserve-already-subscribed",
            customerId: $"cus_{Guid.NewGuid():N}",
            subscriptionId: $"sub_{Guid.NewGuid():N}",
            cancellationToken: token);
        await using var context = support.CreateRetryingTrainerContext(trainer.TrainerId);

        var result = await new BillingCheckoutStore(context).ReserveAsync(
            trainer.TrainerId,
            Guid.NewGuid(),
            SubscriptionTier.Pro,
            BillingTestSupport.Now,
            TimeSpan.FromMinutes(2),
            token);

        Assert.Equal(CheckoutReservationStatus.AlreadySubscribed, result.Status);
    }

    /// <summary>
    /// A fábrica só recusa o cliente enquanto o Stripe está desativado. Com a guarda
    /// invertida a fábrica rejeitava exatamente a configuração ativa, o que tornava
    /// toda a integração inutilizável em produção sem falhar nenhum teste.
    /// </summary>
    [Fact]
    public void ClientFactory_Enabled_ReturnsConfiguredClient()
    {
        using var factory = new StripeClientFactory(Options.Create(EnabledOptions()));

        var client = factory.GetRequiredClient();

        Assert.NotNull(client);
    }

    /// <summary>
    /// A recuperação de uma sessão existente tem de respeitar o kill switch tal como
    /// a criação. Com a guarda invertida, GetSessionAsync tentava falar com o Stripe
    /// precisamente quando a integração estava desligada.
    /// </summary>
    [Fact]
    public async Task CheckoutGateway_GetSession_WhenDisabled_ReturnsDisabled()
    {
        var gateway = new StripeCheckoutGateway(
            new StripeClientFactory(Options.Create(new StripeOptions())),
            Options.Create(new StripeOptions()));

        var outcome = await gateway.GetSessionAsync(
            "cs_test_documentation_only",
            TestContext.Current.CancellationToken);

        Assert.Equal(BillingGatewayStatus.Disabled, outcome.Status);
    }

    private static StripeOptions EnabledOptions() => new()
    {
        Enabled = true,
        SecretKey = "sk_test_documentation_only",
        CurrentWebhookSecret = "whsec_documentation_only",
        StarterPriceId = "price_starter",
        ProPriceId = "price_pro",
        CheckoutSuccessUrl = new Uri("https://app.example.test/billing/success"),
        CheckoutCancelUrl = new Uri("https://app.example.test/billing/cancel"),
        PortalReturnUrl = new Uri("https://app.example.test/settings/billing"),
        BillingManagementUrl = new Uri("https://app.example.test/settings/billing")
    };
}
