using Application.Features.Nutrition.MealPlans.Abstractions;
using Application.Features.Nutrition.MealPlans.Dtos;
using Application.Results;

namespace Application.Features.Nutrition.MealPlans;

/// <summary>Converte resultados de persistência de planos alimentares em resultados da Application.</summary>
internal static class MealPlanStoreResultMapper
{
    internal static Result ToTransitionResult(this MealPlanStoreResult outcome) =>
        outcome.Kind switch
        {
            MealPlanStoreResult.Status.Changed or
            MealPlanStoreResult.Status.AlreadyInRequestedState => Result.Success(),
            MealPlanStoreResult.Status.NotFound =>
                Result.Failure(NutritionErrors.MealPlanNotFound),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };

    internal static Result<MealPlanDetailsDto> ToDetailsFailure(
        this MealPlanStoreResult outcome) =>
        outcome.Kind switch
        {
            MealPlanStoreResult.Status.NotFound =>
                Result<MealPlanDetailsDto>.Failure(NutritionErrors.MealPlanNotFound),
            MealPlanStoreResult.Status.ClientNotFound =>
                Result<MealPlanDetailsDto>.Failure(NutritionErrors.ClientNotFound),
            MealPlanStoreResult.Status.StructureReferenceNotFound =>
                Result<MealPlanDetailsDto>.Failure(
                    NutritionErrors.MealPlanStructureReferenceNotFound),
            MealPlanStoreResult.Status.CatalogReferenceNotFound =>
                Result<MealPlanDetailsDto>.Failure(NutritionErrors.CatalogReferenceNotFound),
            MealPlanStoreResult.Status.CatalogReferenceInactive =>
                Result<MealPlanDetailsDto>.Failure(NutritionErrors.CatalogReferenceInactive),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
}
