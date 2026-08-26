using Application.Features.Billing.Abstractions;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Persistence.Billing;

/// <summary>Persiste a associaçaõ do primeiro customer com controlo concorrente.</summary>
internal sealed class BillingCheckoutStore : IBillingCheckoutStore
{
    private readonly PtManagerDbContext _dbContext;

    public BillingCheckoutStore(PtManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<CheckoutContext?> GetCheckoutContextAsync(
        Guid trainerId,
        CancellationToken cancellationToken
    )
    {
        var subscription = await _dbContext.TrainerSubscriptions
            .AsNoTracking()
            .Where(sub => sub.TrainerId == trainerId)
            .Select(sub => new { sub.StripeCustomerId })
            .SingleOrDefaultAsync(cancellationToken);

        if (subscription is null)
            return null;

        var email = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == trainerId && user.Role == "trainer" &&
                user.IsActive && !user.IsDeleted)
            .Select(user => user.Email)
            .SingleOrDefaultAsync(cancellationToken);

        return email is null
            ? null
            : new CheckoutContext(email, subscription.StripeCustomerId);
    }

    public async Task<string?> GetCustomerIdAsync(
        Guid trainerId,
        CancellationToken cancellationToken
    ) => await _dbContext.TrainerSubscriptions
        .AsNoTracking()
        .Where(sub => sub.TrainerId == trainerId)
        .Select(sub => sub.StripeCustomerId)
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<LinkPaymentCustomerStoreResult> LinkCustomerAsync(
        Guid trainerId,
        string providerCustomerId,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var normalizedCustomerId = providerCustomerId.Trim();
        var status = LinkPaymentCustomerStoreStatus.Linked;
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        try
        {
            await strategy.ExecuteInTransactionAsync(
                async operationToken =>
                {
                    // O delegate pode ser reexecutado pela strategy; limpar o
                    // tracker garante que cada tentativa parte de estado limpo.
                    _dbContext.ChangeTracker.Clear();
                    var subscription = await _dbContext.TrainerSubscriptions
                        .FromSqlInterpolated(
                            $"SELECT * FROM trainer_subscriptions WHERE trainer_id = {trainerId} FOR UPDATE")
                        .SingleOrDefaultAsync(operationToken);
                    if (subscription is null)
                    {
                        status = LinkPaymentCustomerStoreStatus.SubscriptionNotFound;
                        return;
                    }

                    if (subscription.StripeCustomerId is not null)
                    {
                        status = subscription.StripeCustomerId == normalizedCustomerId
                            ? LinkPaymentCustomerStoreStatus.AlreadyLinkedToSameCustomer
                            : LinkPaymentCustomerStoreStatus.LinkedToDifferentCustomer;
                        return;
                    }

                    subscription.LinkStripeCustomer(providerCustomerId, now);
                    status = LinkPaymentCustomerStoreStatus.Linked;
                    await _dbContext.SaveChangesAsync(
                        acceptAllChangesOnSuccess: false,
                        operationToken);
                },
                verifyToken => _dbContext.TrainerSubscriptions
                    .AsNoTracking()
                    .AnyAsync(sub => sub.TrainerId == trainerId &&
                        sub.StripeCustomerId == normalizedCustomerId, verifyToken),
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(LinkPaymentCustomerStoreStatus.ConcurrencyConflict);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException postgres &&
            postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
            postgres.ConstraintName == "uq_trainer_subscriptions_stripe_customer")
        {
            return new(LinkPaymentCustomerStoreStatus.LinkedToDifferentCustomer);
        }

        _dbContext.ChangeTracker.Clear();
        return new(status);
    }
}
