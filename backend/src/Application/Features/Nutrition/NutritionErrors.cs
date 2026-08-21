using Application.Errors;

namespace Application.Features.Nutrition;

/// <summary>Disponibiliza erros estáveis partilhados pelos casos de uso de Nutrition.</summary>
public static class NutritionErrors
{
    public static readonly Error FoodNotFound = Error.Create(
        "food_not_found",
        ErrorCategory.NotFound,
        "The food was not found."
    );

    public static readonly Error GlobalFoodReadOnly = Error.Create(
        "global_food_read_only",
        ErrorCategory.Forbidden,
        "Global foods are read-only."
    );

    public static readonly Error MealPlanNotFound = Error.Create(
        "meal_plan_not_found",
        ErrorCategory.NotFound,
        "The meal plan was not found."
    );

    public static readonly Error MealPlanStructureReferenceNotFound = Error.Create(
        "meal_plan_structure_reference_not_found",
        ErrorCategory.NotFound,
        "A referenced meal plan node was not found."
    );

    public static readonly Error ClientNotFound = Error.Create(
        "nutrition_client_not_found",
        ErrorCategory.NotFound,
        "The client was not found."
    );

    public static readonly Error CatalogReferenceNotFound = Error.Create(
        "nutrition_catalog_reference_not_found",
        ErrorCategory.NotFound,
        "A food or supplement reference was not found."
    );

    public static readonly Error CatalogReferenceInactive = Error.Create(
        "nutrition_catalog_reference_inactive",
        ErrorCategory.Conflict,
        "A new catalog reference is inactive."
    );

    public static readonly Error TrainerOnly = Error.Create(
        "food_trainer_only",
        ErrorCategory.Forbidden,
        "Only a personal trainer can manage private foods."
    );

    public static readonly Error MealPlanTrainerOnly = Error.Create(
        "meal_plan_trainer_only",
        ErrorCategory.Forbidden,
        "Only a personal trainer can manage meal plans."
    );

    public static readonly Error AdministratorOnly = Error.Create(
        "food_administrator_only",
        ErrorCategory.Forbidden,
        "Only an authorized superuser can manage global foods."
    );

    public static readonly Error FoodInactive = Error.Create(
        "food_inactive",
        ErrorCategory.Conflict,
        "An archived food cannot be modified. Reactivate it first."
    );

    public static readonly Error GlobalFoodReferenced = Error.Create(
        "global_food_referenced",
        ErrorCategory.Conflict,
        "A referenced global food cannot be updated. Historical plans must not changed."
    );

    public static readonly Error GlobalFoodHasReferences = Error.Create(
        "global_food_has_references",
        ErrorCategory.Conflict,
        "A referenced global food cannot be deleted. Archived instead."
    );

    public static Error FoodIdRequired() => Error.Validation([
        new ValidationError("FoodId", "food_id_required", "Food ID is required.")
    ]);

    public static Error MealPlanIdRequired() => Error.Validation([
        new ValidationError(
            "MealPlanId",
            "meal_plan_id_required",
            "Meal plan ID is required."
        )
    ]);
}
