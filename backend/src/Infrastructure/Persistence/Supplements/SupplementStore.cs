using Application.Features.Supplements.Abstractions;
using Domain.Entities.Supplements;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Supplements;

/// <summary>Persiste suplementos privados sem permitir escrita global.</summary>
internal sealed class SupplementStore : ISupplementStore
{
    private readonly PtManagerDbContext _dbContext;

    public SupplementStore(PtManagerDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task AddAsync(Supplement supplement, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(supplement);
        _dbContext.Supplements.Add(supplement);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<SupplementStoreResult> UpdateAsync(
        Guid trainerId,
        Guid supplementId,
        string name,
        string? description,
        string unitOfMeasure,
        string servingSize,
        string timing,
        string? trainerNotes,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var supplement = await _dbContext.Supplements.SingleOrDefaultAsync(
            item => item.Id == supplementId, cancellationToken);

        if (supplement is null)
            return SupplementStoreResult.For(SupplementStoreResult.Status.NotFound);

        if (!supplement.OwnerTrainerId.HasValue)
            return SupplementStoreResult.For(SupplementStoreResult.Status.GlobalReadOnly);

        if (supplement.OwnerTrainerId.Value != trainerId)
            return SupplementStoreResult.For(SupplementStoreResult.Status.NotFound);

        if (!supplement.IsActive)
            return SupplementStoreResult.For(SupplementStoreResult.Status.Inactive);

        supplement.Update(
            name, description, unitOfMeasure, servingSize, timing, trainerNotes, now);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return SupplementStoreResult.WithSupplement(
            SupplementStoreResult.Status.Updated, supplement);
    }

    public async Task<SupplementStoreResult> SetActiveAsync(
        Guid trainerId,
        Guid supplementId,
        bool isActive,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var affected = await _dbContext.Supplements
            .Where(item => item.Id == supplementId && item.OwnerTrainerId == trainerId)
            .Where(item => item.IsActive != isActive)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.IsActive, isActive)
                    .SetProperty(item => item.UpdatedAt, now),
                cancellationToken);

        if (affected == 1)
            return SupplementStoreResult.For(SupplementStoreResult.Status.Changed);
        if (affected > 1)
            throw new InvalidOperationException("A Supplement ID must identify at most one row.");

        var visible = await _dbContext.Supplements
            .AsNoTracking()
            .Where(item => item.Id == supplementId)
            .Select(item => new { item.OwnerTrainerId, item.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        if (visible is null)
            return SupplementStoreResult.For(SupplementStoreResult.Status.NotFound);
        if (!visible.OwnerTrainerId.HasValue)
            return SupplementStoreResult.For(SupplementStoreResult.Status.GlobalReadOnly);

        return visible.OwnerTrainerId.Value == trainerId && visible.IsActive == isActive
            ? SupplementStoreResult.For(SupplementStoreResult.Status.AlreadyInRequestedState)
            : SupplementStoreResult.For(SupplementStoreResult.Status.NotFound);
    }
}
