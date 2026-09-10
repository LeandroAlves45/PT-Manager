using Application.Features.Billing.Abstractions;
using Domain.Entities.Billing;
using Domain.ValueObjects;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Persistence.Billing;

/// <summary>Persiste a associaçaõ do primeiro customer com controlo concorrente.</summary>
internal sealed class BillingCheckoutStore : IBillingCheckoutStore
{
    private static readonly TimeSpan MinimumStripeTrial = TimeSpan.FromHours(48);
    private readonly PtManagerDbContext _dbContext;

    public BillingCheckoutStore(PtManagerDbContext dbContext) =>
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    public async Task<CheckoutReservationResult> ReserveAsync(
        Guid trainerId,
        Guid clientOperationId,
        SubscriptionTier tier,
        DateTime now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken
    )
    {
        var result = new CheckoutReservationResult(CheckoutReservationStatus.ConcurrencyConflict);
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteInTransactionAsync(async token =>
            {
                _dbContext.ChangeTracker.Clear();
                var reservationContext = await (
                    from subscriptionRow in _dbContext.TrainerSubscriptions.AsNoTracking()
                    join user in _dbContext.Users.AsNoTracking()
                        on subscriptionRow.TrainerId equals user.Id
                    where subscriptionRow.TrainerId == trainerId &&
                        user.Role == "trainer" && user.IsActive && !user.IsDeleted
                    select new { Subscription = subscriptionRow, TrainerEmail = user.Email })
                    .SingleOrDefaultAsync(token);
                if (reservationContext is null)
                {
                    result = new(CheckoutReservationStatus.SubscriptionNotFound);
                    return;
                }

                var subscription = reservationContext.Subscription;
                if (subscription.IsExemptFromBilling)
                {
                    result = new(CheckoutReservationStatus.BillingExempt);
                    return;
                }

                if (subscription.StripeSubscriptionId is not null &&
                    subscription.Status != SubscriptionStatus.Cancelled &&
                    subscription.Status != SubscriptionStatus.Inactive)
                {
                    result = new(CheckoutReservationStatus.AlreadySubscribed);
                    return;
                }

                var operations = await _dbContext.BillingCheckoutOperations
                    .Where(operation => operation.TrainerId == trainerId &&
                        (operation.ClientOperationId == clientOperationId ||
                            operation.Status == BillingCheckoutOperationStatus.Pending ||
                            operation.Status == BillingCheckoutOperationStatus.Created))
                    .OrderBy(operation => operation.CreatedAt)
                    .ToListAsync(token);

                foreach (var created in operations.Where(operation =>
                    operation.Status == BillingCheckoutOperationStatus.Created &&
                    operation.StripeSessionExpiresAt <= now))
                    created.MarkExpired(now);

                var same = operations.SingleOrDefault(operation =>
                    operation.ClientOperationId == clientOperationId);
                if (same is not null && same.Tier != tier)
                {
                    result = new(CheckoutReservationStatus.SameKeyDifferentTier);
                    return;
                }

                var activeOther = operations.FirstOrDefault(operation =>
                    operation.ClientOperationId != clientOperationId &&
                    (operation.Status == BillingCheckoutOperationStatus.Created ||
                        operation.Status == BillingCheckoutOperationStatus.Pending));
                if (activeOther?.Status == BillingCheckoutOperationStatus.Pending &&
                    activeOther.LeaseExpiresAt <= now)
                {
                    activeOther.MarkAbandoned(now);
                    activeOther = null;
                }
                if (activeOther is not null)
                {
                    result = new(CheckoutReservationStatus.AnotherOperationActive);
                    return;
                }

                if (same is not null)
                {
                    if (same.Status == BillingCheckoutOperationStatus.Created &&
                        same.StripeSessionExpiresAt > now)
                    {
                        result = new(
                            CheckoutReservationStatus.ResumeCreated,
                            same.Id,
                            ProviderCustomerId: subscription.StripeCustomerId,
                            Tier: same.Tier,
                            EffectiveTrialEndsAt: same.EffectiveTrialEndsAt,
                            ProviderSessionId: same.StripeCheckoutSessionId);
                        return;
                    }

                    if (same.Status == BillingCheckoutOperationStatus.Pending)
                    {
                        if (same.LeaseExpiresAt > now)
                        {
                            same.RenewActiveLease(now.Add(leaseDuration), now);
                            result = Success(
                                same,
                                same.LeaseOwnerId!.Value,
                                reservationContext.TrainerEmail,
                                subscription.StripeCustomerId);
                            await _dbContext.SaveChangesAsync(token);
                            return;
                        }

                        var resumeOwner = Guid.NewGuid();
                        same.AcquireExpiredLease(resumeOwner, now.Add(leaseDuration), now);
                        result = Success(
                            same,
                            resumeOwner,
                            reservationContext.TrainerEmail,
                            subscription.StripeCustomerId);
                        await _dbContext.SaveChangesAsync(token);
                        return;
                    }

                    if (same.Status == BillingCheckoutOperationStatus.Failed ||
                        same.Status == BillingCheckoutOperationStatus.Expired)
                    {
                        var reopenedOwner = Guid.NewGuid();
                        same.ReopenForRetry(reopenedOwner, now.Add(leaseDuration), now);
                        result = Success(
                            same,
                            reopenedOwner,
                            reservationContext.TrainerEmail,
                            subscription.StripeCustomerId);
                        await _dbContext.SaveChangesAsync(token);
                        return;
                    }

                    result = new(CheckoutReservationStatus.AlreadySubscribed);
                    return;
                }

                var owner = Guid.NewGuid();
                var operation = BillingCheckoutOperation.CreatePending(
                    trainerId,
                    clientOperationId,
                    tier,
                    owner,
                    now.Add(leaseDuration),
                    EffectiveTrialEnd(subscription.TrialEndsAt, now),
                    now);
                _dbContext.BillingCheckoutOperations.Add(operation);
                await _dbContext.SaveChangesAsync(token);
                result = Success(
                    operation,
                    owner,
                    reservationContext.TrainerEmail,
                    subscription.StripeCustomerId);
            },
            _ => Task.FromResult(result.Status is CheckoutReservationStatus.Acquired or
                CheckoutReservationStatus.ResumeCreated),
                cancellationToken);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException postgres &&
            postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return new(CheckoutReservationStatus.AnotherOperationActive);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(CheckoutReservationStatus.ConcurrencyConflict);
        }
        finally
        {
            _dbContext.ChangeTracker.Clear();
        }

        return result;
    }

    public async Task<CheckoutMutationStatus> LinkCustomerAsync(
        Guid operationId,
        Guid leaseOwnerId,
        string providerCustomerId,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var normalizedCustomerId = providerCustomerId.Trim();
        var result = CheckoutMutationStatus.NotFound;
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteInTransactionAsync(
                async token =>
                {
                    _dbContext.ChangeTracker.Clear();
                    var operation = await _dbContext.BillingCheckoutOperations
                        .SingleOrDefaultAsync(item => item.Id == operationId, token);
                    if (operation is null)
                    {
                        result = CheckoutMutationStatus.NotFound;
                        return;
                    }

                    if (operation.LeaseOwnerId != leaseOwnerId ||
                        operation.LeaseExpiresAt <= now)
                    {
                        result = CheckoutMutationStatus.LeaseLost;
                        return;
                    }

                    // O lock serializa associações concorrentes do mesmo personal trainer.
                    // A transação termina antes de o handler criar a Checkout Session.
                    var subscription = await _dbContext.TrainerSubscriptions
                        .FromSqlInterpolated(
                            $"SELECT * FROM trainer_subscriptions WHERE trainer_id = {operation.TrainerId} FOR UPDATE")
                        .SingleOrDefaultAsync(token);
                    if (subscription is null)
                    {
                        result = CheckoutMutationStatus.NotFound;
                        return;
                    }

                    if (subscription.StripeCustomerId is not null &&
                        subscription.StripeCustomerId != normalizedCustomerId)
                    {
                        result = CheckoutMutationStatus.CustomerConflict;
                        return;
                    }

                    if (subscription.StripeCustomerId == normalizedCustomerId)
                    {
                        result = CheckoutMutationStatus.AlreadyApplied;
                        return;
                    }

                    subscription.LinkStripeCustomer(normalizedCustomerId, now);
                    await _dbContext.SaveChangesAsync(token);
                    result = CheckoutMutationStatus.Applied;
                },
                verifyToken => _dbContext.TrainerSubscriptions
                    .AsNoTracking()
                    .AnyAsync(sub =>
                        sub.StripeCustomerId == normalizedCustomerId,
                        verifyToken),
                cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException postgres &&
            postgres.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            result = CheckoutMutationStatus.CustomerConflict;
        }
        finally
        {
            _dbContext.ChangeTracker.Clear();
        }

        return result;
    }

    public async Task<CheckoutMutationStatus> MarkSessionCreatedAsync(
        Guid operationId,
        Guid leaseOwnerId,
        string providerSessionId,
        DateTime sessionExpiresAt,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var operation = await _dbContext.BillingCheckoutOperations
            .SingleOrDefaultAsync(item => item.Id == operationId, cancellationToken);
        if (operation is null)
            return CheckoutMutationStatus.NotFound;

        if (operation.Status == BillingCheckoutOperationStatus.Created &&
            operation.StripeCheckoutSessionId == providerSessionId)
            return CheckoutMutationStatus.AlreadyApplied;
        if (operation.LeaseOwnerId != leaseOwnerId ||
            operation.LeaseExpiresAt <= now)
            return CheckoutMutationStatus.LeaseLost;

        operation.MarkCreated(leaseOwnerId, providerSessionId, sessionExpiresAt, now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return CheckoutMutationStatus.Applied;
    }

    public async Task MarkFailedAsync(
        Guid operationId,
        Guid leaseOwnerId,
        string failureCode,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var operation = await _dbContext.BillingCheckoutOperations
            .SingleOrDefaultAsync(item => item.Id == operationId, cancellationToken);
        if (operation is null || operation.LeaseOwnerId != leaseOwnerId ||
            operation.LeaseExpiresAt <= now)
            return;

        operation.MarkFailed(leaseOwnerId, failureCode, now);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> GetCustomerIdAsync(
        Guid trainerId,
        CancellationToken cancellationToken
    ) => await _dbContext.TrainerSubscriptions
        .AsNoTracking()
        .Where(sub => sub.TrainerId == trainerId)
        .Select(sub => sub.StripeCustomerId)
        .SingleOrDefaultAsync(cancellationToken);

    private static CheckoutReservationResult Success(
        BillingCheckoutOperation operation,
        Guid owner,
        string email,
        string? customerId) => new(
            CheckoutReservationStatus.Acquired,
            operation.Id,
            owner,
            email,
            customerId,
            operation.Tier,
            operation.EffectiveTrialEndsAt);

    private static DateTime? EffectiveTrialEnd(DateTime? localEnd, DateTime now)
    {
        if (!localEnd.HasValue || localEnd.Value <= now)
            return null;

        var minimum = now.Add(MinimumStripeTrial);
        return localEnd.Value < minimum ? minimum : localEnd.Value;
    }
}
