using Application.Errors;

namespace Application.Features.Nutrition;

/// <summary>Disponibiliza erros estáveis partilhados pelos casos de uso de Nutrition.</summary>
public static class NutritionErrors
{
    public static readonly Error FoodNotFound = Error.Create(
        code: "food_not_found",
        category: ErrorCategory.NotFound,
        description: "The food was not found."
    );

    public static readonly Error GlobalFoodReadOnly = Error.Create(
        code: "global_food_read_only",
        category: ErrorCategory.Forbidden,
        description: "Global foods are read-only."
    );

    public static readonly Error MealPlanNotFound = Error.Create(
        code: "meal_plan_not_found",
        category: ErrorCategory.NotFound,
        description: "The meal plan was not found."
    );

    public static readonly Error MealPlanStructureReferenceNotFound = Error.Create(
        code: "meal_plan_structure_reference_not_found",
        category: ErrorCategory.NotFound,
        description: "A referenced meal plan node was not found."
    );

    public static readonly Error ClientNotFound = Error.Create(
        code: "nutrition_client_not_found",
        category: ErrorCategory.NotFound,
        description: "The client was not found."
    );

    public static readonly Error CatalogReferenceNotFound = Error.Create(
        code: "nutrition_catalog_reference_not_found",
        category: ErrorCategory.NotFound,
        description: "A food or supplement reference was not found."
    );

    public static readonly Error CatalogReferenceInactive = Error.Create(
        code: "nutrition_catalog_reference_inactive",
        category: ErrorCategory.Conflict,
        description: "A new catalog reference is inactive."
    );
}
