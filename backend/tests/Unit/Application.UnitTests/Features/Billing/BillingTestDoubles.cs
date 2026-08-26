using Application.Common.Abstractions;
using Application.Features.Billing.Abstractions;
using Application.Features.Billing.Dtos;
using Application.Features.Billing.Webhooks;
using FluentValidation;

namespace Application.UnitTests.Features.Billing;

internal sealed class BillingValidValidator<T> : AbstractValidator<T> { }

internal sealed class BillingClock(DateTime utcNow) : IClock
{
    public DateTime UtcNow { get; } = utcNow;
}

internal sealed class BillingTenant : ITenantContext
{
    public Guid? TrainerId { get; init; }
    public Guid? UserId { get; init; }
    public string? Role { get; init; }
    public TenantOrigin Origin { get; init; } = TenantOrigin.Http;
    public bool IsAdministrative { get; init; }
}

internal sealed class SubscriptionStoreStub : ISubscriptionQueryStore
{
    public SubscriptionDto? Value { get; set; }
    public int Calls { get; private set; }
    public Guid? RequestedTrainerId { get; private set; }

    public Task<SubscriptionDto?> GetSubscriptionAsync(
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        Calls++;
        RequestedTrainerId = trainerId;
        return Task.FromResult(Value);
    }
}

internal sealed class CheckoutStoreStub : IBillingCheckoutStore
{
    public CheckoutContext? Context { get; set; }
    public string? CustomerId { get; set; }
    public LinkPaymentCustomerStoreResult LinkResult { get; set; } =
        new(LinkPaymentCustomerStoreStatus.Linked);
    public int CheckoutContextCalls { get; private set; }
    public int LinkCustomerCalls { get; private set; }
    public int CustomerIdCalls { get; private set; }
    public Guid? RequestedTrainerId { get; private set; }
    public string? LinkedProviderCustomerId { get; private set; }
    public DateTime? LinkedAt { get; private set; }

    public Task<CheckoutContext?> GetCheckoutContextAsync(
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        CheckoutContextCalls++;
        RequestedTrainerId = trainerId;
        return Task.FromResult(Context);
    }

    public Task<LinkPaymentCustomerStoreResult> LinkCustomerAsync(
        Guid trainerId,
        string providerCustomerId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        LinkCustomerCalls++;
        RequestedTrainerId = trainerId;
        LinkedProviderCustomerId = providerCustomerId;
        LinkedAt = now;
        return Task.FromResult(LinkResult);
    }

    public Task<string?> GetCustomerIdAsync(
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        CustomerIdCalls++;
        RequestedTrainerId = trainerId;
        return Task.FromResult(CustomerId);
    }
}

internal sealed class CheckoutGatewayStub : ICheckoutGateway
{
    public CreatedCheckout Checkout { get; set; } = new(
        new Uri("https://pay.example/checkout"),
        "cus_created");
    public int Calls { get; private set; }
    public CreateCheckoutRequest? Request { get; private set; }

    public Task<CreatedCheckout> CreateCheckoutAsync(
        CreateCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        Calls++;
        Request = request;
        return Task.FromResult(Checkout);
    }
}

internal sealed class PortalGatewayStub : ICustomerPortalGateway
{
    public Uri Url { get; set; } = new("https://pay.example/portal");
    public int Calls { get; private set; }
    public CreateCustomerPortalRequest? Request { get; private set; }

    public Task<Uri> CreateCustomerPortalAsync(
        CreateCustomerPortalRequest request,
        CancellationToken cancellationToken)
    {
        Calls++;
        Request = request;
        return Task.FromResult(Url);
    }
}

internal sealed class ReconciliationGatewayStub : ISubscriptionReconciliationGateway
{
    public ProviderSubscriptionSnapshot? Snapshot { get; set; }
    public int Calls { get; private set; }
    public string? RequestedProviderCustomerId { get; private set; }
    public string? RequestedProviderSubscriptionId { get; private set; }

    public Task<ProviderSubscriptionSnapshot?> GetSubscriptionSnapshotAsync(
        string? providerCustomerId,
        string? providerSubscriptionId,
        CancellationToken cancellationToken)
    {
        Calls++;
        RequestedProviderCustomerId = providerCustomerId;
        RequestedProviderSubscriptionId = providerSubscriptionId;
        return Task.FromResult(Snapshot);
    }
}

internal sealed class PaymentEventStoreStub : IPaymentEventStore
{
    public CommitPaymentEventStoreResult Result { get; set; } =
        new(CommitPaymentEventStoreStatus.Processed);
    public int Calls { get; private set; }
    public NormalizedPaymentEvent? PaymentEvent { get; private set; }
    public ProviderSubscriptionSnapshot? Snapshot { get; private set; }
    public DateTime? CommittedAt { get; private set; }

    public Task<CommitPaymentEventStoreResult> CommitAsync(
        NormalizedPaymentEvent paymentEvent,
        ProviderSubscriptionSnapshot? reconciledSnapshot,
        DateTime now,
        CancellationToken cancellationToken)
    {
        Calls++;
        PaymentEvent = paymentEvent;
        Snapshot = reconciledSnapshot;
        CommittedAt = now;
        return Task.FromResult(Result);
    }
}
