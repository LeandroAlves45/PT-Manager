using Application.Features.Billing.Abstractions;
using Infrastructure.IntegrationTests.Support;
using Infrastructure.Persistence.Billing;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.IntegrationTests.Billing;

[Collection(PostgresCollection.Name)]
public sealed class BillingCheckoutStoreTests(PostgresContainerFixture database)
{
    [Fact]
    public async Task CheckoutContext_WhenRequestedForAnotherTenant_IsNotVisible()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var owner = await support.SeedTrainerAsync(
            "checkout-owner",
            cancellationToken: cancellationToken);
        var other = await support.SeedTrainerAsync(
            "checkout-other",
            cancellationToken: cancellationToken);
        await using var context = support.CreateRetryingTrainerContext(owner.TrainerId);
        var store = new BillingCheckoutStore(context);

        var result = await store.GetCheckoutContextAsync(
            other.TrainerId,
            cancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task SubscriptionQuery_WhenRequestedForAnotherTenant_IsNotVisible()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var owner = await support.SeedTrainerAsync(
            "query-owner",
            cancellationToken: cancellationToken);
        var other = await support.SeedTrainerAsync(
            "query-other",
            cancellationToken: cancellationToken);
        await using var context = support.CreateRetryingTrainerContext(owner.TrainerId);
        var store = new SubscriptionQueryStore(context);

        var result = await store.GetSubscriptionAsync(
            other.TrainerId,
            cancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task LinkCustomer_WithRetryingExecutionStrategy_PersistsAssociation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        var trainer = await support.SeedTrainerAsync(
            "link-retry",
            cancellationToken: cancellationToken);
        await using var context = support.CreateRetryingTrainerContext(trainer.TrainerId);
        var store = new BillingCheckoutStore(context);

        var result = await store.LinkCustomerAsync(
            trainer.TrainerId,
            " cus_retry_strategy ",
            BillingTestSupport.Now.AddMinutes(1),
            cancellationToken);

        await using var verification = database.CreateContext(trainer.TrainerId);
        var persistedCustomerId = await verification.TrainerSubscriptions
            .Where(subscription => subscription.TrainerId == trainer.TrainerId)
            .Select(subscription => subscription.StripeCustomerId)
            .SingleAsync(cancellationToken);
        Assert.Equal(LinkPaymentCustomerStoreStatus.Linked, result.Kind);
        Assert.Equal("cus_retry_strategy", persistedCustomerId);
    }

    [Fact]
    public async Task LinkCustomer_WhenCustomerBelongsToAnotherTrainer_ReturnsConflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var support = new BillingTestSupport(database);
        await support.SeedTrainerAsync(
            "customer-owner",
            customerId: "cus_unique_owner",
            cancellationToken: cancellationToken);
        var target = await support.SeedTrainerAsync(
            "customer-target",
            cancellationToken: cancellationToken);
        await using var context = support.CreateRetryingTrainerContext(target.TrainerId);
        var store = new BillingCheckoutStore(context);

        var result = await store.LinkCustomerAsync(
            target.TrainerId,
            "cus_unique_owner",
            BillingTestSupport.Now.AddMinutes(1),
            cancellationToken);

        Assert.Equal(LinkPaymentCustomerStoreStatus.LinkedToDifferentCustomer, result.Kind);
    }
}
