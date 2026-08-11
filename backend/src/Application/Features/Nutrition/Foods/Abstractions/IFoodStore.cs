using Domain.Entities.Nutrition;

namespace Application.Features.Nutrition.Foods.Abstractions;

/// <summary>Persiste mutações tenant-safe do catálogo de alimentos.</summary>
public interface IFoodStore
{
    Task AddAsync(Food food, CancellationToken cancellationToken);

    Task<Food?> GetOwnedForReadAsync(Guid foodId, CancellationToken cancellationToken);

    Task<FoodStoreResult> UpdateAsync(
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
    );

    Task<FoodStoreResult> SetActiveAsync(
        Guid foodId,
        Guid trainerId,
        bool isActive,
        DateTime now,
        CancellationToken cancellationToken
    );
}
