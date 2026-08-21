using Application.Features.Nutrition.Foods.Abstractions;
using Application.Features.Nutrition.Foods.Dtos;
using Application.Results;

namespace Application.Features.Nutrition.Foods;

/// <summary>
/// Converte resultados de persistência de alimentos globais em resultados da Application.
/// </summary>
internal static class GlobalFoodStoreResultMapper
{
    /// <summary>Traduz outcomes administrativos de Food para Result.</summary>
    internal static Result<GlobalFoodDto> ToDtoResult(this GlobalFoodStoreResult outcome) =>
        outcome.Kind switch
        {
            GlobalFoodStoreResult.Status.Created or
            GlobalFoodStoreResult.Status.Updated =>
                Result<GlobalFoodDto>.Success(outcome.Food!.ToGlobalDto()),
            GlobalFoodStoreResult.Status.NotFound =>
                Result<GlobalFoodDto>.Failure(NutritionErrors.FoodNotFound),
            GlobalFoodStoreResult.Status.Inactive =>
                Result<GlobalFoodDto>.Failure(NutritionErrors.FoodInactive),
            GlobalFoodStoreResult.Status.Referenced =>
                Result<GlobalFoodDto>.Failure(NutritionErrors.GlobalFoodReferenced),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };

    /// <summary>Traduz outcomes de transição (Archive/Reactivate/Delete) de Food para Result.</summary>
    internal static Result ToTransitionResult(this GlobalFoodStoreResult outcome) =>
        outcome.Kind switch
        {
            GlobalFoodStoreResult.Status.Changed or
            GlobalFoodStoreResult.Status.Deleted or
            GlobalFoodStoreResult.Status.AlreadyInRequestedState => Result.Success(),
            GlobalFoodStoreResult.Status.NotFound =>
                Result.Failure(NutritionErrors.FoodNotFound),
            GlobalFoodStoreResult.Status.Inactive =>
                Result.Failure(NutritionErrors.FoodInactive),
            GlobalFoodStoreResult.Status.HasReferences =>
                Result.Failure(NutritionErrors.GlobalFoodHasReferences),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
}
