using Application.Features.Nutrition.Foods.Abstractions;
using Application.Features.Nutrition.Foods.Dtos;
using Application.Results;

namespace Application.Features.Nutrition.Foods;

/// <summary>Converte resultados de persistência de alimentos em resultados da Application.</summary>
internal static class FoodStoreResultMapper
{
    internal static Result ToTransitionResult(this FoodStoreResult outcome) =>
        outcome.Kind switch
        {
            FoodStoreResult.Status.Changed or
            FoodStoreResult.Status.AlreadyInRequestedState => Result.Success(),
            FoodStoreResult.Status.NotFound => Result.Failure(NutritionErrors.FoodNotFound),
            FoodStoreResult.Status.GlobalReadOnly =>
                Result.Failure(NutritionErrors.GlobalFoodReadOnly),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };

    internal static Result<FoodDto> ToUpdateResult(this FoodStoreResult outcome) =>
        outcome.Kind switch
        {
            FoodStoreResult.Status.Updated =>
                Result<FoodDto>.Success(outcome.Food!.ToDto()),
            FoodStoreResult.Status.NotFound =>
                Result<FoodDto>.Failure(NutritionErrors.FoodNotFound),
            FoodStoreResult.Status.GlobalReadOnly =>
                Result<FoodDto>.Failure(NutritionErrors.GlobalFoodReadOnly),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
}
