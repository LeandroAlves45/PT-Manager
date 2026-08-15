using Application.Features.Packs.PackTypes.Abstractions;
using Domain.Entities.Billing;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Packs;

/// <summary>Persiste tipos de packs privados do tenant.</summary>
public sealed class PackTypeStore : IPackTypeStore
{
    private readonly PtManagerDbContext _dbContext;

    public PackTypeStore(PtManagerDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task AddAsync(
        PackType packType,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(packType);
        _dbContext.PackTypes.Add(packType);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PackTypeStoreResult> UpdateAsync(
        Guid packTypeId,
        Guid trainerId,
        string name,
        int sessionCount,
        int priceCents,
        string currency,
        int? expectedDurationDays,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var packType = await _dbContext.PackTypes
            .SingleOrDefaultAsync(candidate => candidate.Id == packTypeId
                && candidate.OwnerTrainerId == trainerId,
                cancellationToken
            );

        if (packType is null)
            return PackTypeStoreResult.ForNotFound();

        packType.Update(
            name,
            sessionCount,
            priceCents,
            currency,
            expectedDurationDays,
            now
        );
        await _dbContext.SaveChangesAsync(cancellationToken);
        return PackTypeStoreResult.ForUpdated(packType);
    }

    public async Task<PackTypeStoreResult> SetActiveAsync(
        Guid packTypeId,
        Guid trainerId,
        bool isActive,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var affected = await _dbContext.PackTypes
            .Where(pack => pack.Id == packTypeId)
            .Where(pack => pack.OwnerTrainerId == trainerId)
            .Where(pack => pack.IsActive != isActive)
            // Um PackType soft deleted nunca pode ser reativado por este bulk
            // update, que não passa pela invariante Domain EnsureNotDeleted().
            .Where(pack => !pack.IsDeleted)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(pack => pack.IsActive, isActive)
                    .SetProperty(pack => pack.UpdatedAt, now),
                cancellationToken
            );

        if (affected == 1)
            return PackTypeStoreResult.ForChanged();
        if (affected > 1)
            throw new InvalidOperationException("PackType ID must be unique.");

        var current = await _dbContext.PackTypes
            .AsNoTracking()
            .Where(pack => pack.Id == packTypeId)
            .Where(pack => pack.OwnerTrainerId == trainerId)
            .Select(pack => pack.IsActive)
            .SingleOrDefaultAsync(cancellationToken);

        if (!await _dbContext.PackTypes.AnyAsync(
            pack => pack.Id == packTypeId && pack.OwnerTrainerId == trainerId,
            cancellationToken
        ))
            return PackTypeStoreResult.ForNotFound();

        return current == isActive
            ? PackTypeStoreResult.ForAlreadyInRequested()
            : throw new InvalidOperationException(
                "PackType state changed unexpectedly during classification."
            );
    }
}
