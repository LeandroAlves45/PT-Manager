using Application.Common.Abstractions;
using Application.Features.Billing.Abstractions;
using Application.Features.Billing.Dtos;
using Application.Features.Billing.Webhooks;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.UnitTests.Features.Billing;

internal sealed class BillingValidValidator<T> : AbstractValidator<T> { }
internal sealed class BillingClock(DateTime utcNow) : IClock { public DateTime UtcNow { get; } = utcNow; }
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
    public Task<SubscriptionDto?> GetSubscriptionAsync(Guid trainerId, CancellationToken cancellationToken)
    { Calls++; RequestedTrainerId = trainerId; return Task.FromResult(Value); }
}

internal sealed class CheckoutStoreStub : IBillingCheckoutStore
{
    public CheckoutReservationResult Reservation { get; set; } = new(
        CheckoutReservationStatus.SubscriptionNotFound);
    public CheckoutMutationStatus LinkResult { get; set; } = CheckoutMutationStatus.Applied;
    public CheckoutMutationStatus SessionResult { get; set; } = CheckoutMutationStatus.Applied;
    public string? CustomerId { get; set; }
    public int ReserveCalls { get; private set; }
    public int LinkCustomerCalls { get; private set; }
    public int SessionCalls { get; private set; }
    public Guid? RequestedTrainerId { get; private set; }

    public Task<CheckoutReservationResult> ReserveAsync(Guid trainerId, Guid clientOperationId,
        SubscriptionTier tier, DateTime now, TimeSpan leaseDuration, CancellationToken cancellationToken)
    { ReserveCalls++; RequestedTrainerId = trainerId; return Task.FromResult(Reservation); }
    public Task<CheckoutMutationStatus> LinkCustomerAsync(Guid operationId, Guid leaseOwnerId,
        string providerCustomerId, DateTime now, CancellationToken cancellationToken)
    { LinkCustomerCalls++; return Task.FromResult(LinkResult); }
    public Task<CheckoutMutationStatus> MarkSessionCreatedAsync(Guid operationId, Guid leaseOwnerId,
        string providerSessionId, DateTime sessionExpiresAt, DateTime now, CancellationToken cancellationToken)
    { SessionCalls++; return Task.FromResult(SessionResult); }
    public Task MarkFailedAsync(Guid operationId, Guid leaseOwnerId, string failureCode,
        DateTime now, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<string?> GetCustomerIdAsync(Guid trainerId, CancellationToken cancellationToken)
    { RequestedTrainerId = trainerId; return Task.FromResult(CustomerId); }
}

internal sealed class CheckoutGatewayStub : ICheckoutGateway
{
    public EnsureCustomerOutcome Customer { get; set; } = new(BillingGatewayStatus.Success, "cus_created");
    public CheckoutSessionOutcome Checkout { get; set; } = new(BillingGatewayStatus.Success,
        "cs_created", new Uri("https://checkout.stripe.com/example"),
        new DateTime(2026, 9, 1, 13, 0, 0, DateTimeKind.Utc));
    public int Calls { get; private set; }
    public CreateCheckoutRequest? Request { get; private set; }
    public Task<EnsureCustomerOutcome> EnsureCustomerAsync(EnsureCustomerRequest request, CancellationToken cancellationToken)
        => Task.FromResult(Customer);
    public Task<CheckoutSessionOutcome> CreateSessionAsync(CreateCheckoutRequest request, CancellationToken cancellationToken)
    { Calls++; Request = request; return Task.FromResult(Checkout); }
    public Task<CheckoutSessionOutcome> GetSessionAsync(string providerSessionId, CancellationToken cancellationToken)
    { Calls++; return Task.FromResult(Checkout); }
}

internal sealed class PortalGatewayStub : ICustomerPortalGateway
{
    public CustomerPortalOutcome Outcome { get; set; } = new(BillingGatewayStatus.Success,
        new Uri("https://billing.stripe.com/example"));
    public int Calls { get; private set; }
    public CreateCustomerPortalRequest? Request { get; private set; }
    public Task<CustomerPortalOutcome> CreateAsync(CreateCustomerPortalRequest request, CancellationToken cancellationToken)
    { Calls++; Request = request; return Task.FromResult(Outcome); }
}

internal sealed class ReconciliationGatewayStub : ISubscriptionReconciliationGateway
{
    public ProviderSubscriptionSnapshot? Snapshot { get; set; }
    public SubscriptionReconciliationStatus Status { get; set; } = SubscriptionReconciliationStatus.Success;
    public int Calls { get; private set; }
    public string? RequestedProviderCustomerId { get; private set; }
    public string? RequestedProviderSubscriptionId { get; private set; }
    public Task<SubscriptionReconciliationOutcome> GetSubscriptionSnapshotAsync(string? providerCustomerId,
        string? providerSubscriptionId, CancellationToken cancellationToken)
    { Calls++; RequestedProviderCustomerId = providerCustomerId; RequestedProviderSubscriptionId = providerSubscriptionId; return Task.FromResult(new SubscriptionReconciliationOutcome(Status, Snapshot)); }
}

internal sealed class PaymentEventStoreStub : IPaymentEventStore
{
    public bool IsProcessed { get; set; }
    public CommitPaymentEventStoreResult Result { get; set; } = new(CommitPaymentEventStoreStatus.Processed);
    public int Calls { get; private set; }
    public NormalizedPaymentEvent? PaymentEvent { get; private set; }
    public ProviderSubscriptionSnapshot? Snapshot { get; private set; }
    public DateTime? CommittedAt { get; private set; }
    public Task<bool> IsProcessedAsync(string eventId, CancellationToken cancellationToken) =>
        Task.FromResult(IsProcessed);
    public Task<CommitPaymentEventStoreResult> CommitAsync(NormalizedPaymentEvent paymentEvent,
        ProviderSubscriptionSnapshot? reconciledSnapshot, DateTime now, CancellationToken cancellationToken)
    { Calls++; PaymentEvent = paymentEvent; Snapshot = reconciledSnapshot; CommittedAt = now; return Task.FromResult(Result); }
}
