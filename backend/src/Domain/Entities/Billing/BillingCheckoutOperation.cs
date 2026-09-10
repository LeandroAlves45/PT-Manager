using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.Entities.Billing;

/// <summary>
/// Intenção durável que serializa a criação de subscrições Stripe por personal trainer.
/// </summary>
public sealed class BillingCheckoutOperation
{
    public Guid Id { get; private set; }
    public Guid TrainerId { get; private set; }
    public Guid ClientOperationId { get; private set; }
    public SubscriptionTier Tier { get; private set; } = null!;
    public BillingCheckoutOperationStatus Status { get; private set; } = null!;
    public Guid? LeaseOwnerId { get; private set; }
    public DateTime? LeaseExpiresAt { get; private set; }
    public DateTime? EffectiveTrialEndsAt { get; private set; }
    public string? StripeCheckoutSessionId { get; private set; }
    public DateTime? StripeSessionExpiresAt { get; private set; }
    public string? FailureCode { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private BillingCheckoutOperation() { }

    private BillingCheckoutOperation(
        Guid trainerId,
        Guid clientOperationId,
        SubscriptionTier tier,
        Guid leaseOwnerId,
        DateTime leaseExpiresAt,
        DateTime? effectiveTrialEndsAt,
        DateTime now
    )
    {
        Id = Guid.NewGuid();
        TrainerId = trainerId;
        ClientOperationId = clientOperationId;
        Tier = tier;
        Status = BillingCheckoutOperationStatus.Pending;
        LeaseOwnerId = leaseOwnerId;
        LeaseExpiresAt = leaseExpiresAt;
        EffectiveTrialEndsAt = effectiveTrialEndsAt;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public static BillingCheckoutOperation CreatePending(
        Guid trainerId,
        Guid clientOperationId,
        SubscriptionTier tier,
        Guid leaseOwnerId,
        DateTime leaseExpiresAt,
        DateTime? effectiveTrialEndsAt,
        DateTime now
    )
    {
        if (trainerId == Guid.Empty || clientOperationId == Guid.Empty ||
            leaseOwnerId == Guid.Empty)
            throw new DomainException("Checkout operation identifiers are required.");

        ArgumentNullException.ThrowIfNull(tier);
        EnsureUtc(now, nameof(now));
        EnsureUtc(leaseExpiresAt, nameof(leaseExpiresAt));

        if (leaseExpiresAt <= now)
            throw new DomainException("Checkout lease must expire in the future.");
        if (effectiveTrialEndsAt.HasValue)
            EnsureUtc(effectiveTrialEndsAt.Value, nameof(effectiveTrialEndsAt));

        return new BillingCheckoutOperation(
            trainerId,
            clientOperationId,
            tier,
            leaseOwnerId,
            leaseExpiresAt,
            effectiveTrialEndsAt,
            now
        );
    }

    public void AcquireExpiredLease(Guid ownerId, DateTime expiresAt, DateTime now)
    {
        EnsureStatus(BillingCheckoutOperationStatus.Pending);
        if (LeaseExpiresAt > now)
            throw new DomainException("Checkout lease has not expired.");
        if (ownerId == Guid.Empty || expiresAt <= now)
            throw new DomainException("Checkout lease is invalid.");

        EnsureUtc(now, nameof(now));
        EnsureUtc(expiresAt, nameof(expiresAt));

        LeaseOwnerId = ownerId;
        LeaseExpiresAt = expiresAt;
        UpdatedAt = now;
    }

    /// <summary>
    /// O retry com a mesma Idempotency-Key reutiliza o dono da lease ainda válida
    /// e prolonga-a, em vez de tratar o caller original como um concurrente.
    /// </summary>
    public void RenewActiveLease(DateTime expiresAt, DateTime now)
    {
        EnsureStatus(BillingCheckoutOperationStatus.Pending);
        if (LeaseOwnerId is null || LeaseExpiresAt <= now)
            throw new DomainException("Checkout lease has expired.");
        if (expiresAt <= now)
            throw new DomainException("Checkout lease is invalid.");

        EnsureUtc(now, nameof(now));
        EnsureUtc(expiresAt, nameof(expiresAt));

        LeaseExpiresAt = expiresAt;
        UpdatedAt = now;
    }

    /// <summary>
    /// Reabre uma intenção Failed ou Expired na mesma linha, porque o unique
    /// (trainer, client_operation_id) impede inserir outra linha para a mesma chave.
    /// </summary>
    public void ReopenForRetry(Guid ownerId, DateTime expiresAt, DateTime now)
    {
        if (Status != BillingCheckoutOperationStatus.Failed &&
            Status != BillingCheckoutOperationStatus.Expired)
            throw new DomainException("Checkout operation cannot be reopened.");
        if (ownerId == Guid.Empty || expiresAt <= now)
            throw new DomainException("Checkout lease is invalid.");
        EnsureUtc(now, nameof(now));
        EnsureUtc(expiresAt, nameof(expiresAt));

        Status = BillingCheckoutOperationStatus.Pending;
        LeaseOwnerId = ownerId;
        LeaseExpiresAt = expiresAt;
        FailureCode = null;
        StripeCheckoutSessionId = null;
        StripeSessionExpiresAt = null;
        UpdatedAt = now;
    }

    public void MarkCreated(Guid ownerId, string sessionId, DateTime sessionExpiresAt, DateTime now)
    {
        EnsureLease(ownerId, now);
        var normalized = Normalize(sessionId, "Stripe Checkout Session ID is invalid.");
        if (sessionExpiresAt <= now)
            throw new DomainException("Stripe Checkout Session must expire in the future.");

        StripeCheckoutSessionId = normalized;
        StripeSessionExpiresAt = sessionExpiresAt;
        Status = BillingCheckoutOperationStatus.Created;
        LeaseOwnerId = null;
        LeaseExpiresAt = null;
        UpdatedAt = now;
    }

    public void MarkCompleted(DateTime now)
    {
        if (Status != BillingCheckoutOperationStatus.Created)
            throw new DomainException("Only a created Checkout operation can complete.");

        Status = BillingCheckoutOperationStatus.Completed;
        UpdatedAt = now;
    }

    public void MarkExpired(DateTime now)
    {
        if (Status != BillingCheckoutOperationStatus.Created || StripeSessionExpiresAt > now)
            throw new DomainException("Checkout operation has not expired.");

        Status = BillingCheckoutOperationStatus.Expired;
        UpdatedAt = now;
    }

    public void MarkFailed(Guid ownerId, string failureCode, DateTime now)
    {
        EnsureLease(ownerId, now);

        FailureCode = Normalize(failureCode, "Failure code is invalid.");
        Status = BillingCheckoutOperationStatus.Failed;
        LeaseOwnerId = null;
        LeaseExpiresAt = null;
        UpdatedAt = now;
    }

    /// <summary>Liberta uma intenção abandonada apenas depois de a lease expirar.</summary>
    public void MarkAbandoned(DateTime now)
    {
        EnsureStatus(BillingCheckoutOperationStatus.Pending);
        if (LeaseExpiresAt > now)
            throw new DomainException("Checkout lease has not expired.");

        FailureCode = "abandoned_operation";
        Status = BillingCheckoutOperationStatus.Failed;
        LeaseOwnerId = null;
        LeaseExpiresAt = null;
        UpdatedAt = now;
    }

    private void EnsureLease(Guid ownerId, DateTime now)
    {
        EnsureStatus(BillingCheckoutOperationStatus.Pending);
        if (LeaseOwnerId != ownerId || LeaseExpiresAt <= now)
            throw new DomainException("Checkout lease is not owned or has expired.");
    }

    private void EnsureStatus(BillingCheckoutOperationStatus expected)
    {
        if (Status != expected)
            throw new DomainException("Checkout operation is in an invalid state.");
    }

    private static string Normalize(string value, string message)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized) || normalized.Length > 255)
            throw new DomainException(message);
        return normalized;
    }

    private static void EnsureUtc(DateTime value, string parameterName)
    {
        if (value == default || value.Kind != DateTimeKind.Utc)
            throw new DomainException($"{parameterName} must be UTC.");
    }
}
