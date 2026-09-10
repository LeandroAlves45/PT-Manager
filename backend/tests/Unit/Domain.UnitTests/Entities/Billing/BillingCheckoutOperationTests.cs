using Domain.Entities.Billing;
using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.UnitTests.Entities.Billing;

public sealed class BillingCheckoutOperationTests
{
    private static readonly DateTime Now = new(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid TrainerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OperationKey = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid OwnerId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void RenewActiveLease_WhileLeaseIsValid_ExtendsExpiryAndKeepsOwner()
    {
        var operation = CreatePending();

        operation.RenewActiveLease(Now.AddMinutes(10), Now.AddMinutes(1));

        Assert.Equal(BillingCheckoutOperationStatus.Pending, operation.Status);
        Assert.Equal(OwnerId, operation.LeaseOwnerId);
        Assert.Equal(Now.AddMinutes(10), operation.LeaseExpiresAt);
        Assert.Equal(Now.AddMinutes(1), operation.UpdatedAt);
    }

    [Fact]
    public void RenewActiveLease_AfterExpiry_Throws()
    {
        var operation = CreatePending();

        var exception = Assert.Throws<DomainException>(() =>
            operation.RenewActiveLease(Now.AddMinutes(12), Now.AddMinutes(6)));

        Assert.Equal("Checkout lease has expired.", exception.Message);
        Assert.Equal(Now.AddMinutes(5), operation.LeaseExpiresAt);
    }

    [Fact]
    public void ReopenForRetry_FromFailed_ReturnsToPendingWithNewLease()
    {
        var operation = CreatePending();
        operation.MarkFailed(OwnerId, "transient_failure", Now.AddMinutes(1));
        var retryOwner = Guid.Parse("44444444-4444-4444-4444-444444444444");

        operation.ReopenForRetry(retryOwner, Now.AddMinutes(8), Now.AddMinutes(2));

        Assert.Equal(BillingCheckoutOperationStatus.Pending, operation.Status);
        Assert.Equal(retryOwner, operation.LeaseOwnerId);
        Assert.Equal(Now.AddMinutes(8), operation.LeaseExpiresAt);
        Assert.Null(operation.FailureCode);
        Assert.Null(operation.StripeCheckoutSessionId);
    }

    [Fact]
    public void ReopenForRetry_FromExpired_ClearsSessionAndReturnsToPending()
    {
        var operation = CreatePending();
        operation.MarkCreated(OwnerId, "cs_1", Now.AddMinutes(30), Now.AddMinutes(1));
        operation.MarkExpired(Now.AddMinutes(31));
        var retryOwner = Guid.Parse("55555555-5555-5555-5555-555555555555");

        operation.ReopenForRetry(retryOwner, Now.AddMinutes(40), Now.AddMinutes(32));

        Assert.Equal(BillingCheckoutOperationStatus.Pending, operation.Status);
        Assert.Equal(retryOwner, operation.LeaseOwnerId);
        Assert.Null(operation.StripeCheckoutSessionId);
        Assert.Null(operation.StripeSessionExpiresAt);
    }

    [Fact]
    public void ReopenForRetry_FromPending_Throws()
    {
        var operation = CreatePending();

        Assert.Throws<DomainException>(() =>
            operation.ReopenForRetry(Guid.NewGuid(), Now.AddMinutes(10), Now.AddMinutes(1)));
        Assert.Equal(BillingCheckoutOperationStatus.Pending, operation.Status);
    }

    private static BillingCheckoutOperation CreatePending() =>
        BillingCheckoutOperation.CreatePending(
            TrainerId,
            OperationKey,
            SubscriptionTier.Starter,
            OwnerId,
            Now.AddMinutes(5),
            Now.AddDays(7),
            Now);
}
