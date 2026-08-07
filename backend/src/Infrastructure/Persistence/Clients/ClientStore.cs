using Application.Features.Clients.Abstractions;
using Domain.Entities.Clients;
using Domain.ValueObjects;
using Infrastructure.Data;
using Infrastructure.Persistence.Errors;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Clients;

/// <summary>Implementa escritas de Clients e respetivas transações de subscrição.</summary>
internal sealed class ClientStore : IClientStore
{
    private readonly PtManagerDbContext _dbContext;
    private readonly PostgresConstraintTranslator _constraintTranslator;

    /// <summary>Inicializa o store scoped e o translator de constraints.</summary>
    public ClientStore(
        PtManagerDbContext dbContext,
        PostgresConstraintTranslator constraintTranslator)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(constraintTranslator);

        _dbContext = dbContext;
        _constraintTranslator = constraintTranslator;
    }

    /// <inheritdoc/>
    public async Task<CreateClientStoreOutcome> CreateWithSubscriptionSlotAsync(
        Client client,
        Guid trainerId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        if (trainerId == Guid.Empty)
            throw new ArgumentException(
                "Trainer ID is required.",
                nameof(trainerId));

        if (client.OwnerTrainerId != trainerId)
            throw new ArgumentException(
                "Trainer ID must match the client owner.",
                nameof(trainerId));

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        var attempt = 0;

        return await strategy.ExecuteAsync(async () =>
        {
            attempt++;

            // Se a ligação cair durante o commit, a transação pode ter sido
            // confirmada apesar da exceção. O ID é criado pela aplicação e permite
            // confirmar o resultado antes de repetir o insert.
            if (attempt > 1)
            {
                var wasCommitted = await _dbContext.Clients
                    .AsNoTracking()
                    .AnyAsync(existing => existing.Id == client.Id, cancellationToken);

                if (wasCommitted)
                    return CreateClientStoreOutcome.Created;
            }

            return await CreateOnceAsync(
                client,
                trainerId,
                now,
                cancellationToken);
        });
    }

    /// <inheritdoc/>
    public async Task<Client?> GetForUpdateAsync(
        Guid clientId,
        CancellationToken cancellationToken = default)
    {
        if (clientId == Guid.Empty)
            throw new ArgumentException(
                "Client ID is required.",
                nameof(clientId));

        return await _dbContext.Clients
            .SingleOrDefaultAsync(client => client.Id == clientId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SaveClientProfileOutcome> SaveProfileAsync(
        Client client,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return SaveClientProfileOutcome.Updated;
        }
        catch (DbUpdateException exception)
        {
            var translated = _constraintTranslator.TryTranslate(
                exception,
                PersistenceOperation.UpdateClient,
                out var error);
            if (translated && error?.Code == "client_email_already_exists")
                return SaveClientProfileOutcome.DuplicateEmail;
            if (translated && error?.Code == "client_phone_already_exists")
                return SaveClientProfileOutcome.DuplicatePhone;
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<ArchiveClientStoreOutcome> ArchiveAsync(
        Guid clientId,
        Guid trainerId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(clientId, trainerId);

        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(
            () => ArchiveOnceAsync(
                clientId,
                trainerId,
                now,
                cancellationToken));
    }

    /// <inheritdoc/>
    public async Task<ReactivateClientStoreOutcome> ReactivateAsync(
        Guid clientId,
        Guid trainerId,
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        ValidateIdentifiers(clientId, trainerId);

        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(
            () => ReactivateOnceAsync(
                clientId,
                trainerId,
                now,
                cancellationToken));
    }

    private async Task<CreateClientStoreOutcome> CreateOnceAsync(
        Client client,
        Guid trainerId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            _dbContext.Clients.Add(client);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var affectedSubscription = await _dbContext.TrainerSubscriptions
                .Where(subscription => subscription.TrainerId == trainerId)
                .Where(subscription =>
                    subscription.IsExemptFromBilling ||
                    subscription.Status == SubscriptionStatus.Active &&
                    subscription.CurrentClientCount < subscription.ClientLimit)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            subscription => subscription.CurrentClientCount,
                            subscription => subscription.CurrentClientCount + 1)
                        .SetProperty(
                            subscription => subscription.UpdatedAt,
                            now),
                    cancellationToken);

            if (affectedSubscription == 1)
            {
                await transaction.CommitAsync(cancellationToken);
                return CreateClientStoreOutcome.Created;
            }

            var state = await LoadSubscriptionStateAsync(
                trainerId,
                cancellationToken);

            var outcome = MapCreateSubscriptionFailure(state);

            await transaction.RollbackAsync(CancellationToken.None);
            return outcome;
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(CancellationToken.None);

            var translated = _constraintTranslator.TryTranslate(
                exception,
                PersistenceOperation.CreateClient,
                out var error);

            if (translated && error?.Code == "client_email_already_exists")
                return CreateClientStoreOutcome.DuplicateEmail;

            if (translated && error?.Code == "client_phone_already_exists")
                return CreateClientStoreOutcome.DuplicatePhone;

            throw;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<ArchiveClientStoreOutcome> ArchiveOnceAsync(
        Guid clientId,
        Guid trainerId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var affectedClient = await _dbContext.Clients
                .Where(client => client.Id == clientId)
                .Where(client => client.OwnerTrainerId == trainerId)
                .Where(client => client.IsActive && !client.IsDeleted)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(client => client.IsActive, false)
                        .SetProperty(client => client.UpdatedAt, now),
                    cancellationToken);

            if (affectedClient == 0)
            {
                var state = await _dbContext.Clients
                    .AsNoTracking()
                    .Where(client => client.Id == clientId)
                    .Select(client => new ClientActivityState(client.IsActive))
                    .SingleOrDefaultAsync(cancellationToken);

                var outcome = state switch
                {
                    null => ArchiveClientStoreOutcome.NotFound,
                    { IsActive: false } => ArchiveClientStoreOutcome.AlreadyArchived,
                    _ => throw new InvalidOperationException(
                        "Client could not be classified after the archive update")
                };

                await transaction.RollbackAsync(CancellationToken.None);
                return outcome;
            }

            var affectedSubscription = await _dbContext.TrainerSubscriptions
                .Where(subscription => subscription.TrainerId == trainerId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            subscription => subscription.CurrentClientCount,
                            subscription => subscription.CurrentClientCount > 0
                                ? subscription.CurrentClientCount - 1
                                : 0)
                        .SetProperty(
                            subscription => subscription.UpdatedAt,
                            now),
                    cancellationToken);

            if (affectedSubscription == 0)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return ArchiveClientStoreOutcome.SubscriptionMissing;
            }

            await transaction.CommitAsync(cancellationToken);
            return ArchiveClientStoreOutcome.Archived;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<ReactivateClientStoreOutcome> ReactivateOnceAsync(
        Guid clientId,
        Guid trainerId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var affectedClient = await _dbContext.Clients
                .Where(client => client.Id == clientId)
                .Where(client => client.OwnerTrainerId == trainerId)
                .Where(client => !client.IsActive && !client.IsDeleted)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(client => client.IsActive, true)
                        .SetProperty(client => client.UpdatedAt, now),
                    cancellationToken);

            if (affectedClient == 0)
            {
                var stateReactivate = await _dbContext.Clients
                    .AsNoTracking()
                    .Where(client => client.Id == clientId)
                    .Select(client => new ClientActivityState(client.IsActive))
                    .SingleOrDefaultAsync(cancellationToken);

                var outcomeReactivate = stateReactivate switch
                {
                    null => ReactivateClientStoreOutcome.NotFound,
                    { IsActive: true } => ReactivateClientStoreOutcome.AlreadyActive,
                    _ => throw new InvalidOperationException(
                        "Client could not be classified after the reactivate update")
                };

                await transaction.RollbackAsync(CancellationToken.None);
                return outcomeReactivate;
            }

            var affectedSubscription = await _dbContext.TrainerSubscriptions
                .Where(subscription => subscription.TrainerId == trainerId)
                .Where(subscription =>
                    subscription.IsExemptFromBilling ||
                    subscription.Status == SubscriptionStatus.Active &&
                    subscription.CurrentClientCount < subscription.ClientLimit)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(
                            subscription => subscription.CurrentClientCount,
                            subscription => subscription.CurrentClientCount + 1)
                        .SetProperty(
                            subscription => subscription.UpdatedAt,
                            now),
                    cancellationToken);

            if (affectedSubscription == 1)
            {
                await transaction.CommitAsync(cancellationToken);
                return ReactivateClientStoreOutcome.Reactivated;
            }

            var state = await LoadSubscriptionStateAsync(
                trainerId,
                cancellationToken);

            var outcome = MapReactivateSubscriptionFailure(state);

            await transaction.RollbackAsync(CancellationToken.None);
            return outcome;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private Task<SubscriptionState?> LoadSubscriptionStateAsync(
        Guid trainerId,
        CancellationToken cancellationToken)
    {
        return _dbContext.TrainerSubscriptions
            .AsNoTracking()
            .Where(subscription => subscription.TrainerId == trainerId)
            .Select(subscription => new SubscriptionState(
                subscription.Status,
                subscription.IsExemptFromBilling,
                subscription.CurrentClientCount,
                subscription.ClientLimit))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static CreateClientStoreOutcome MapCreateSubscriptionFailure(
        SubscriptionState? state)
    {
        if (state is null)
            return CreateClientStoreOutcome.SubscriptionMissing;

        if (state.IsExemptFromBilling)
            throw new InvalidOperationException(
                "An exempt subscription should have satisfied the capacity update.");

        if (state.Status == SubscriptionStatus.Inactive)
            return CreateClientStoreOutcome.SubscriptionInactive;

        if (state.Status == SubscriptionStatus.Suspended)
            return CreateClientStoreOutcome.SubscriptionSuspended;

        if (state.Status == SubscriptionStatus.Cancelled)
            return CreateClientStoreOutcome.SubscriptionCancelled;

        if (state.Status == SubscriptionStatus.Active &&
            state.CurrentClientCount >= state.ClientLimit)
            return CreateClientStoreOutcome.ClientLimitReached;

        throw new InvalidOperationException(
            "The subscription should have satisfied the client capacity update.");
    }

    private static ReactivateClientStoreOutcome MapReactivateSubscriptionFailure(
        SubscriptionState? state)
    {
        if (state is null)
            return ReactivateClientStoreOutcome.SubscriptionMissing;

        if (state.IsExemptFromBilling)
            throw new InvalidOperationException(
                "An exempt subscription should have satisfied the capacity update.");

        if (state.Status == SubscriptionStatus.Inactive)
            return ReactivateClientStoreOutcome.SubscriptionInactive;

        if (state.Status == SubscriptionStatus.Suspended)
            return ReactivateClientStoreOutcome.SubscriptionSuspended;

        if (state.Status == SubscriptionStatus.Cancelled)
            return ReactivateClientStoreOutcome.SubscriptionCancelled;

        if (state.Status == SubscriptionStatus.Active &&
            state.CurrentClientCount >= state.ClientLimit)
            return ReactivateClientStoreOutcome.ClientLimitReached;

        throw new InvalidOperationException(
            "The subscription should have satisfied the client capacity update.");
    }

    private static void ValidateIdentifiers(Guid clientId, Guid trainerId)
    {
        if (clientId == Guid.Empty)
            throw new ArgumentException(
                "Client ID is required.",
                nameof(clientId));

        if (trainerId == Guid.Empty)
            throw new ArgumentException(
                "Trainer ID is required.",
                nameof(trainerId));
    }

    private sealed record ClientActivityState(bool IsActive);

    private sealed record SubscriptionState(
        SubscriptionStatus Status,
        bool IsExemptFromBilling,
        int CurrentClientCount,
        int ClientLimit);
}
