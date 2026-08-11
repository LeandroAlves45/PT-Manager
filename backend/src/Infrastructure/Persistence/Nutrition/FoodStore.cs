using Application.Features.Nutrition.Foods.Abstractions;
using Domain.Entities.Nutrition;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Nutrition;

/// <summary>Persiste alimentos privados sob Global Query Filters.</summary>
internal sealed class FoodStore : IFoodStore
{
    private readonly PtManagerDbContext _dbContext;

    public FoodStore(PtManagerDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public async Task AddAsync(Food food, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(food);
        _dbContext.Foods.Add(food);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Food?> GetOwnedForReadAsync(
        Guid foodId,
        CancellationToken cancellationToken
    ) => _dbContext.Foods.SingleOrDefaultAsync(
        food => food.Id == foodId && food.OwnerTrainerId != null,
        cancellationToken
    );

    public async Task<FoodStoreResult> UpdateAsync(
        Guid foodId,
        Guid trainerId,
        string name,
        string? description,
        decimal protein,
        decimal carbs,
        decimal fats,
        decimal? fiber,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var food = await _dbContext.Foods.SingleOrDefaultAsync(
            candidate => candidate.Id == foodId,
            cancellationToken
        );

        if (food is null)
            return FoodStoreResult.ForNotFound();
        if (food.OwnerTrainerId is null)
            return FoodStoreResult.ForGlobalReadOnly();
        if (food.OwnerTrainerId != trainerId)
            return FoodStoreResult.ForNotFound();

        food.Update(name, description, protein, carbs, fats, fiber, now);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await _dbContext.Entry(food).ReloadAsync(cancellationToken);
        return FoodStoreResult.ForUpdated(food);
    }

    public async Task<FoodStoreResult> SetActiveAsync(
        Guid foodId,
        Guid trainerId,
        bool isActive,
        DateTime now,
        CancellationToken cancellationToken
    )
    {
        var affected = await _dbContext.Foods
            .Where(food => food.Id == foodId && food.OwnerTrainerId == trainerId)
            .Where(food => food.IsActive != isActive)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(food => food.IsActive, isActive)
                    .SetProperty(food => food.UpdatedAt, now),
                cancellationToken
            );
        if (affected == 1)
            return FoodStoreResult.ForChanged();
        if (affected > 1)
            throw new InvalidOperationException(
                "A Food ID must identify at most one row."
            );

        var visible = await _dbContext.Foods
            .AsNoTracking()
            .Where(food => food.Id == foodId)
            .Select(food => new { food.OwnerTrainerId, food.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        if (visible is null)
            return FoodStoreResult.ForNotFound();
        if (visible.OwnerTrainerId is null)
            return FoodStoreResult.ForGlobalReadOnly();
        return visible.OwnerTrainerId == trainerId && visible.IsActive == isActive
            ? FoodStoreResult.ForAlreadyRequested()
            : FoodStoreResult.ForNotFound();
    }
}
