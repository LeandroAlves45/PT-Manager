using Application.Features.Billing;
using Application.Features.Billing.Abstractions;
using Application.Features.Billing.CreateCheckout;
using Application.Features.Billing.CreateCustomerPortal;
using Application.Features.Billing.Dtos;
using Application.Features.Billing.GetSubscription;

namespace Application.UnitTests.Features.Billing;

public sealed class BillingHandlerTests
{
    private static readonly DateTime Now = new(
        2026,
        9,
        1,
        12,
        0,
        0,
        DateTimeKind.Utc);

    [Fact]
    public async Task GetSubscription_ClientActor_IsRejectedBeforeStore()
    {
        var store = new SubscriptionStoreStub();
        var handler = new GetSubscriptionHandler(ClientTenant(), store);

        var result = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(BillingErrors.TrainerOnly.Code, result.Error!.Code);
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task GetSubscription_MissingSubscription_ReturnsStableError()
    {
        var handler = new GetSubscriptionHandler(TrainerTenant(out _), new SubscriptionStoreStub());

        var result = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(BillingErrors.SubscriptionNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task GetSubscription_ExistingSubscription_ReturnsProjectionForAuthenticatedTrainer()
    {
        var expected = new SubscriptionDto("ACTIVE", "PRO", 100, 4, Now.AddDays(7));
        var store = new SubscriptionStoreStub { Value = expected };
        var handler = new GetSubscriptionHandler(TrainerTenant(out var trainerId), store);

        var result = await handler.HandleAsync(TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Same(expected, result.Value);
        Assert.Equal(trainerId, store.RequestedTrainerId);
    }

    [Fact]
    public async Task Checkout_ClientActor_IsRejectedBeforeStoreAndGateway()
    {
        var store = new CheckoutStoreStub();
        var gateway = new CheckoutGatewayStub();
        var handler = CreateCheckoutHandler(ClientTenant(), store, gateway);

        var result = await handler.HandleAsync(
            ValidCheckoutCommand(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(BillingErrors.TrainerOnly.Code, result.Error!.Code);
        Assert.Equal(0, store.CheckoutContextCalls);
        Assert.Equal(0, gateway.Calls);
    }

    [Fact]
    public async Task Checkout_MissingSubscription_DoesNotCallProvider()
    {
        var store = new CheckoutStoreStub();
        var gateway = new CheckoutGatewayStub();
        var handler = CreateCheckoutHandler(TrainerTenant(out _), store, gateway);

        var result = await handler.HandleAsync(
            ValidCheckoutCommand(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(BillingErrors.SubscriptionNotFound.Code, result.Error!.Code);
        Assert.Equal(0, gateway.Calls);
        Assert.Equal(0, store.LinkCustomerCalls);
    }

    [Fact]
    public async Task Checkout_ValidContext_BuildsProviderRequestFromTrustedState()
    {
        var tenant = TrainerTenant(out var trainerId);
        var operationId = Guid.NewGuid();
        var store = new CheckoutStoreStub
        {
            Context = new CheckoutContext("trainer@example.test", "cus_existing")
        };
        var gateway = new CheckoutGatewayStub();
        var handler = CreateCheckoutHandler(tenant, store, gateway);
        var command = new CreateCheckoutCommand(
            operationId,
            "STARTER",
            new Uri("https://app.example/success"),
            new Uri("https://app.example/cancel"));

        var result = await handler.HandleAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(trainerId, gateway.Request!.TrainerId);
        Assert.Equal(operationId, gateway.Request.OperationId);
        Assert.Equal("cus_existing", gateway.Request.ProviderCustomerId);
        Assert.Equal("trainer@example.test", gateway.Request.TrainerEmail);
        Assert.Equal("STARTER", gateway.Request.Tier.Value);
        Assert.Equal(command.SuccessUrl, gateway.Request.SuccessUrl);
        Assert.Equal(command.CancelUrl, gateway.Request.CancelUrl);
    }

    [Fact]
    public async Task Checkout_RepeatedOperation_DerivesStableTenantBoundIdempotencyKey()
    {
        var tenant = TrainerTenant(out var trainerId);
        var operationId = Guid.NewGuid();
        var store = new CheckoutStoreStub
        {
            Context = new CheckoutContext("trainer@example.test", null)
        };
        var gateway = new CheckoutGatewayStub();
        var handler = CreateCheckoutHandler(tenant, store, gateway);
        var command = ValidCheckoutCommand(operationId);

        var first = await handler.HandleAsync(command, TestContext.Current.CancellationToken);
        var firstKey = gateway.Request!.IdempotencyKey;
        var second = await handler.HandleAsync(command, TestContext.Current.CancellationToken);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal($"checkout:{trainerId:N}:{operationId:N}", firstKey);
        Assert.Equal(firstKey, gateway.Request!.IdempotencyKey);
    }

    [Fact]
    public async Task Checkout_ProviderCreatesCustomer_LinksItUsingCurrentTime()
    {
        var tenant = TrainerTenant(out var trainerId);
        var store = new CheckoutStoreStub
        {
            Context = new CheckoutContext("trainer@example.test", null)
        };
        var gateway = new CheckoutGatewayStub
        {
            Checkout = new CreatedCheckout(new Uri("https://pay.example/session"), "cus_new")
        };
        var handler = CreateCheckoutHandler(tenant, store, gateway);

        var result = await handler.HandleAsync(
            ValidCheckoutCommand(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(trainerId, store.RequestedTrainerId);
        Assert.Equal("cus_new", store.LinkedProviderCustomerId);
        Assert.Equal(Now, store.LinkedAt);
    }

    [Theory]
    [InlineData(LinkPaymentCustomerStoreStatus.Linked)]
    [InlineData(LinkPaymentCustomerStoreStatus.AlreadyLinkedToSameCustomer)]
    public async Task Checkout_LinkAccepted_ReturnsProviderUrl(
        LinkPaymentCustomerStoreStatus status)
    {
        var expected = new Uri("https://pay.example/session");
        var store = LinkedCheckoutStore(status);
        var gateway = new CheckoutGatewayStub
        {
            Checkout = new CreatedCheckout(expected, "cus_new")
        };
        var handler = CreateCheckoutHandler(TrainerTenant(out _), store, gateway);

        var result = await handler.HandleAsync(
            ValidCheckoutCommand(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData(
        LinkPaymentCustomerStoreStatus.SubscriptionNotFound,
        "billing_subscription_not_found")]
    [InlineData(
        LinkPaymentCustomerStoreStatus.LinkedToDifferentCustomer,
        "billing_customer_conflict")]
    [InlineData(
        LinkPaymentCustomerStoreStatus.ConcurrencyConflict,
        "billing_concurrency_conflict")]
    public async Task Checkout_LinkRejected_MapsStableError(
        LinkPaymentCustomerStoreStatus status,
        string expectedCode)
    {
        var handler = CreateCheckoutHandler(
            TrainerTenant(out _),
            LinkedCheckoutStore(status),
            new CheckoutGatewayStub());

        var result = await handler.HandleAsync(
            ValidCheckoutCommand(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error!.Code);
    }

    [Fact]
    public async Task Portal_ClientActor_IsRejectedBeforeStoreAndGateway()
    {
        var store = new CheckoutStoreStub();
        var gateway = new PortalGatewayStub();
        var handler = CreatePortalHandler(ClientTenant(), store, gateway);

        var result = await handler.HandleAsync(
            ValidPortalCommand(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(BillingErrors.TrainerOnly.Code, result.Error!.Code);
        Assert.Equal(0, store.CustomerIdCalls);
        Assert.Equal(0, gateway.Calls);
    }

    [Fact]
    public async Task Portal_MissingCustomer_DoesNotCallProvider()
    {
        var store = new CheckoutStoreStub();
        var gateway = new PortalGatewayStub();
        var handler = CreatePortalHandler(TrainerTenant(out _), store, gateway);

        var result = await handler.HandleAsync(
            ValidPortalCommand(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(BillingErrors.CustomerNotLinked.Code, result.Error!.Code);
        Assert.Equal(0, gateway.Calls);
    }

    [Fact]
    public async Task Portal_LinkedCustomer_BuildsTenantBoundProviderRequest()
    {
        var tenant = TrainerTenant(out var trainerId);
        var operationId = Guid.NewGuid();
        var returnUrl = new Uri("https://app.example/settings/billing");
        var store = new CheckoutStoreStub { CustomerId = "cus_existing" };
        var gateway = new PortalGatewayStub();
        var handler = CreatePortalHandler(tenant, store, gateway);

        var result = await handler.HandleAsync(
            new CreateCustomerPortalCommand(operationId, returnUrl),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(trainerId, gateway.Request!.TrainerId);
        Assert.Equal(operationId, gateway.Request.OperationId);
        Assert.Equal("cus_existing", gateway.Request.ProviderCustomerId);
        Assert.Equal(returnUrl, gateway.Request.ReturnUrl);
        Assert.Equal($"portal:{trainerId:N}:{operationId:N}", gateway.Request.IdempotencyKey);
    }

    [Fact]
    public async Task Portal_ProviderSuccess_ReturnsProviderUrl()
    {
        var expected = new Uri("https://pay.example/customer-portal");
        var gateway = new PortalGatewayStub { Url = expected };
        var handler = CreatePortalHandler(
            TrainerTenant(out _),
            new CheckoutStoreStub { CustomerId = "cus_existing" },
            gateway);

        var result = await handler.HandleAsync(
            ValidPortalCommand(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Value);
    }

    private static CreateCheckoutHandler CreateCheckoutHandler(
        BillingTenant tenant,
        CheckoutStoreStub store,
        CheckoutGatewayStub gateway) => new(
            new BillingValidValidator<CreateCheckoutCommand>(),
            tenant,
            new BillingClock(Now),
            store,
            gateway);

    private static CreateCustomerPortalHandler CreatePortalHandler(
        BillingTenant tenant,
        CheckoutStoreStub store,
        PortalGatewayStub gateway) => new(
            new BillingValidValidator<CreateCustomerPortalCommand>(),
            tenant,
            store,
            gateway);

    private static CheckoutStoreStub LinkedCheckoutStore(
        LinkPaymentCustomerStoreStatus status) => new()
        {
            Context = new CheckoutContext("trainer@example.test", null),
            LinkResult = new LinkPaymentCustomerStoreResult(status)
        };

    private static BillingTenant TrainerTenant(out Guid trainerId)
    {
        trainerId = Guid.NewGuid();
        return new BillingTenant
        {
            TrainerId = trainerId,
            UserId = Guid.NewGuid(),
            Role = "trainer"
        };
    }

    private static BillingTenant ClientTenant() => new()
    {
        TrainerId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Role = "client"
    };

    private static CreateCheckoutCommand ValidCheckoutCommand(Guid? operationId = null) => new(
        operationId ?? Guid.NewGuid(),
        "PRO",
        new Uri("https://app.example/success"),
        new Uri("https://app.example/cancel"));

    private static CreateCustomerPortalCommand ValidPortalCommand() => new(
        Guid.NewGuid(),
        new Uri("https://app.example/billing"));
}
