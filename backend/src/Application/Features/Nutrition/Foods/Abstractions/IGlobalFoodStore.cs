namespace Application.Features.Nutrition.Foods.Abstractions;

/// <summary>Persiste mutações globais de alimentos e a respetiva auditoria na mesma transação.</summary>
public interface IGlobalFoodStore
{
    Task<GlobalFoodStoreResult> CreateAsync(
        Guid actorUserId,
        string name,
        string? description,
        decimal protein,
        decimal carbs,
        decimal fats,
        decimal? fiber,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<GlobalFoodStoreResult> UpdateAsync(
        Guid actorUserId,
        Guid foodId,
        string name,
        string? description,
        decimal protein,
        decimal carbs,
        decimal fats,
        decimal? fiber,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<GlobalFoodStoreResult> SetActiveAsync(
        Guid actorUserId,
        Guid foodId,
        bool isActive,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<GlobalFoodStoreResult> DeleteAsync(
        Guid actorUserId,
        Guid foodId,
        DateTime now,
        CancellationToken cancellationToken
    );
}
