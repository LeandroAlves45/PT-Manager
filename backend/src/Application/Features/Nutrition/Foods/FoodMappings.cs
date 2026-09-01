using Application.Features.Nutrition.Foods.Abstractions;
using Application.Features.Nutrition.Foods.Dtos;
using Application.Results;
using Domain.Entities.Nutrition;

namespace Application.Features.Nutrition.Foods;

/// <summary>Converte Food em contratos da Application sem AutoMapper.</summary>
public static class FoodMappings
{
    /// <summary>Mapeia valores por 100g e oculta o identificador do tenant.</summary>
    public static FoodDto ToDto(this Food food)
    {
        ArgumentNullException.ThrowIfNull(food);

        return new FoodDto(
            food.Id,
            food.OwnerTrainerId is null ? "global" : "private",
            food.Name,
            food.Description,
            food.Protein,
            food.Carbs,
            food.Fats,
            food.Kcal,
            food.Fiber,
            food.IsActive,
            food.PlatformEnforcementStatus.Value,
            food.PlatformEnforcementReason?.Value,
            food.CreatedAt,
            food.UpdatedAt
        );
    }

    /// <summary>Mapeia o alimento global para o contrato administrativo.</summary>
    public static GlobalFoodDto ToGlobalDto(this Food food)
    {
        ArgumentNullException.ThrowIfNull(food);

        return new GlobalFoodDto(
            food.Id,
            food.Name,
            food.Description,
            food.Protein,
            food.Carbs,
            food.Fats,
            food.Kcal,
            food.Fiber,
            food.IsActive,
            food.CreatedAt,
            food.UpdatedAt
        );
    }
}
