using Domain.Entities.Nutrition;

namespace Application.Features.Nutrition.Foods.Abstractions;

/// <summary>Representa outcomes esperados da administração global de alimentos.</summary>
public sealed class GlobalFoodStoreResult
{
    public enum Status
    {
        Created,
        Updated,
        Changed,
        Deleted,
        AlreadyInRequestedState,
        NotFound,
        Inactive,
        Referenced,
        HasReferences
    }

    public Status Kind { get; }
    public Food? Food { get; }

    private GlobalFoodStoreResult(Status kind, Food? food)
    {
        Kind = kind;
        Food = food;
    }

    public static GlobalFoodStoreResult WithFood(Status kind, Food food)
    {
        ArgumentNullException.ThrowIfNull(food);
        return new GlobalFoodStoreResult(kind, food);
    }

    public static GlobalFoodStoreResult For(Status kind) => new(kind, null);
}
