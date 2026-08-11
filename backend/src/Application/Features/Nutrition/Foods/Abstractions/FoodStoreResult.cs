using Domain.Entities.Nutrition;

namespace Application.Features.Nutrition.Foods.Abstractions;

/// <summary>Representa resultados esperados de uma mutação de alimentos.</summary>
public sealed class FoodStoreResult
{
    public enum Status
    {
        Updated,
        Changed,
        AlreadyInRequestedState,
        NotFound,
        GlobalReadOnly
    }

    public Status Kind { get; }
    public Food? Food { get; }

    private FoodStoreResult(Status kind, Food? food)
    {
        Kind = kind;
        Food = food;
    }

    public static FoodStoreResult ForUpdated(Food food)
    {
        ArgumentNullException.ThrowIfNull(food);
        return new FoodStoreResult(Status.Updated, food);
    }

    public static FoodStoreResult ForChanged() => new(Status.Changed, null);
    public static FoodStoreResult ForAlreadyRequested() =>
        new(Status.AlreadyInRequestedState, null);
    public static FoodStoreResult ForNotFound() => new(Status.NotFound, null);
    public static FoodStoreResult ForGlobalReadOnly() =>
        new(Status.GlobalReadOnly, null);
}
