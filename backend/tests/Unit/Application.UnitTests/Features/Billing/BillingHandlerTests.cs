using Application.Features.Billing;
using Application.Features.Billing.Abstractions;
using Application.Features.Billing.CreateCheckout;
using Application.Features.Billing.CreateCustomerPortal;
using Application.Features.Billing.Dtos;
using Application.Features.Billing.GetSubscription;
using Domain.ValueObjects;

namespace Application.UnitTests.Features.Billing;

public sealed class BillingHandlerTests
{
    private static readonly DateTime Now = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GetSubscription_ClientActor_IsRejectedBeforeStore()
    {
        var store = new SubscriptionStoreStub();
        var result = await new GetSubscriptionHandler(ClientTenant(), store)
            .HandleAsync(TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(BillingErrors.TrainerOnly.Code, result.Error!.Code);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task GetSubscription_Pro_ReturnsNullLimit()
    {
        var expected = new SubscriptionDto("ACTIVE", "PRO", null, 40, null);
        var store = new SubscriptionStoreStub { Value = expected };
        var result = await new GetSubscriptionHandler(TrainerTenant(out _), store)
            .HandleAsync(TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ClientLimit);
    }

    [Fact]
    public async Task Checkout_Conflict_DoesNotCallStripe()
    {
        var store = new CheckoutStoreStub
        {
            Reservation = new(CheckoutReservationStatus.AnotherOperationActive)
        };
        var gateway = new CheckoutGatewayStub();
        var result = await CheckoutHandler(TrainerTenant(out _), store, gateway)
            .HandleAsync(new CreateCheckoutCommand(Guid.NewGuid(), "PRO"), TestContext.Current.CancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal(BillingErrors.ActiveCheckoutExists.Code, result.Error!.Code);
        Assert.Equal(0, gateway.Calls);
    }

    [Fact]
    public async Task Checkout_ExistingCustomer_CreatesSessionWithStableKey()
    {
        var operationId = Guid.NewGuid();
        var internalId = Guid.NewGuid();
        var leaseOwner = Guid.NewGuid();
        var store = new CheckoutStoreStub
        {
            Reservation = new(CheckoutReservationStatus.Acquired, internalId, leaseOwner,
                "trainer@example.test", "cus_existing", SubscriptionTier.Pro)
        };
        var gateway = new CheckoutGatewayStub();
        var result = await CheckoutHandler(TrainerTenant(out _), store, gateway)
            .HandleAsync(new CreateCheckoutCommand(operationId, "PRO"), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal($"billing:checkout:{operationId:N}", gateway.Request!.IdempotencyKey);
        Assert.Equal("cus_existing", gateway.Request.ProviderCustomerId);
        Assert.Equal(1, store.SessionCalls);
    }

    [Fact]
    public async Task Portal_UsesPersistedCustomerWithoutReturnUrl()
    {
        var operationId = Guid.NewGuid();
        var store = new CheckoutStoreStub { CustomerId = "cus_existing" };
        var gateway = new PortalGatewayStub();
        var result = await PortalHandler(TrainerTenant(out _), store, gateway)
            .HandleAsync(new CreateCustomerPortalCommand(operationId), TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        Assert.Equal("cus_existing", gateway.Request!.ProviderCustomerId);
        Assert.Equal($"billing:portal:{operationId:N}", gateway.Request.IdempotencyKey);
        Assert.DoesNotContain("ReturnUrl", gateway.Request.GetType().GetProperties().Select(property => property.Name));
    }

    private static CreateCheckoutHandler CheckoutHandler(BillingTenant tenant, CheckoutStoreStub store,
        CheckoutGatewayStub gateway) => new(new BillingValidValidator<CreateCheckoutCommand>(), tenant,
            new BillingClock(Now), store, gateway);
    private static CreateCustomerPortalHandler PortalHandler(BillingTenant tenant, CheckoutStoreStub store,
        PortalGatewayStub gateway) => new(new BillingValidValidator<CreateCustomerPortalCommand>(), tenant, store, gateway);
    private static BillingTenant TrainerTenant(out Guid trainerId)
    { trainerId = Guid.NewGuid(); return new() { TrainerId = trainerId, UserId = Guid.NewGuid(), Role = "trainer" }; }
    private static BillingTenant ClientTenant() => new() { TrainerId = Guid.NewGuid(), UserId = Guid.NewGuid(), Role = "client" };
}
