using System.Data;
using FluentValidation;

namespace Application.Features.Nutrition.MealPlans;

/// <summary>Valida forma, limites e unicidade interna da árvore alimentar.</summary>
public sealed class MealPlanStructureInputValidator : AbstractValidator<MealPlanStructureInput>
{
    private const int MaximumMeals = 20;
    private const int MaximumItemsPerMeal = 50;
    private const int MaximumSupplementsPerMeal = 20;

    /// <summary>Configura regras recursivas para criação e reconciliação.</summary>
    public MealPlanStructureInputValidator(bool requireNewIdentifiers)
    {
        RuleFor(structure => structure.Meals)
            .NotNull()
            .WithErrorCode("meal_plan_meals_required")
            .Must(meals => meals is null || meals.Count <= MaximumMeals)
            .WithErrorCode("meal_plan_meals_limit");

        RuleForEach(structure => structure.Meals)
            .SetValidator(new MealInputValidator(requireNewIdentifiers));

        RuleFor(structure => structure.Meals)
            .Must(meals => meals is null
                || HaveUniqueValues(meals.Select(meal => meal.OrderNumber)))
            .WithErrorCode("meal_plan_meal_order_duplicate");

        // No Update (requireNewIdentifiers: false), o cliente pode reenviar Ids de meals
        // existentes; sem esta regra, duas entradas com o mesmo Id passavam a validação
        // e só seriam apanhadas (ou não) pelo store na reconciliação.
        RuleFor(structure => structure.Meals)
            .Must(meals => meals is null
                || HaveUniqueNonNullIds(meals.Select(meal => meal.Id)))
            .WithErrorCode("meal_plan_meal_id_duplicate");
    }

    private static bool HaveUniqueNonNullIds(IEnumerable<Guid?> values)
    {
        var identifiers = values.Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
        return identifiers.Distinct().Count() == identifiers.Length;
    }

    private static bool HaveUniqueValues<T>(IEnumerable<T> values) where T : notnull
    {
        var materialized = values.ToArray();
        return materialized.Distinct().Count() == materialized.Length;
    }

    private sealed class MealInputValidator : AbstractValidator<MealPlanStructureInput.MealInput>
    {
        public MealInputValidator(bool requireNewIdentifiers)
        {
            ConfigureIdentifierRule(requireNewIdentifiers);

            RuleFor(meal => meal.MealType)
                .NotEmpty()
                .WithErrorCode("meal_plan_meal_type_required")
                .MaximumLength(50)
                .WithErrorCode("meal_plan_meal_type_too_long");

            RuleFor(meal => meal.OrderNumber)
                .GreaterThan(0)
                .WithErrorCode("meal_plan_meal_order_invalid");

            RuleFor(meal => meal.Items)
                .NotNull()
                .WithErrorCode("meal_plan_items_required")
                .Must(items => items is null || items.Count <= MaximumItemsPerMeal)
                .WithErrorCode("meal_plan_items_limit");

            RuleFor(meal => meal.Supplements)
                .NotNull()
                .WithErrorCode("meal_plan_supplements_required")
                .Must(supplements => supplements is null || supplements.Count <= MaximumSupplementsPerMeal)
                .WithErrorCode("meal_plan_supplements_limit");

            RuleForEach(meal => meal.Items)
                .SetValidator(new ItemInputValidator(requireNewIdentifiers));
            RuleForEach(meal => meal.Supplements)
                .SetValidator(new SupplementInputValidator(requireNewIdentifiers));

            RuleFor(meal => meal.Items)
                .Must(items => items is null
                    || HaveUniqueNonNullIds(items.Select(item => item.Id)))
                .WithErrorCode("meal_plan_item_id_duplicate");
            RuleFor(meal => meal.Items)
                .Must(items => items is null
                    || HaveUniqueValues(items.Select(item => item.OrderNumber)))
                .WithErrorCode("meal_plan_item_order_duplicate");

            RuleFor(meal => meal.Supplements)
                .Must(supplements => supplements is null
                    || HaveUniqueNonNullIds(supplements.Select(supplement => supplement.Id)))
                .WithErrorCode("meal_plan_supplement_id_duplicate");
            RuleFor(meal => meal.Supplements)
                .Must(supplements => supplements is null
                    || HaveUniqueValues(supplements.Select(supplement => supplement.OrderNumber)))
                .WithErrorCode("meal_plan_supplement_order_duplicate");
            RuleFor(meal => meal.Supplements)
                .Must(supplements => supplements is null
                    || HaveUniqueValues(supplements.Select(supplement => supplement.SupplementId)))
                .WithErrorCode("meal_plan_supplement_reference_duplicate");
        }

        private void ConfigureIdentifierRule(bool requireNewIdentifiers)
        {
            if (requireNewIdentifiers)
            {
                RuleFor(meal => meal.Id)
                    .Null()
                    .WithErrorCode("meal_plan_create_id_forbidden");
                return;
            }

            RuleFor(meal => meal.Id)
                .Must(id => !id.HasValue || id.Value != Guid.Empty)
                .WithErrorCode("meal_plan_meal_id_invalid");
        }
    }

    private sealed class ItemInputValidator : AbstractValidator<MealPlanStructureInput.ItemInput>
    {
        public ItemInputValidator(bool requireNewIdentifiers)
        {
            if (requireNewIdentifiers)
            {
                RuleFor(item => item.Id)
                    .Null()
                    .WithErrorCode("meal_plan_create_id_forbidden");
            }
            else
            {
                RuleFor(item => item.Id)
                    .Must(id => !id.HasValue || id.Value != Guid.Empty)
                    .WithErrorCode("meal_plan_item_id_invalid");
            }

            RuleFor(item => item.FoodId)
                .NotEmpty()
                .WithErrorCode("meal_plan_food_id_required");
            RuleFor(item => item.QuantityInGrams)
                .GreaterThan(0m)
                .WithErrorCode("meal_plan_item_quantity_invalid");
            RuleFor(item => item.OrderNumber)
                .GreaterThan(0)
                .WithErrorCode("meal_plan_item_order_invalid");
        }
    }

    private sealed class SupplementInputValidator : AbstractValidator<MealPlanStructureInput.SupplementInput>
    {
        public SupplementInputValidator(bool requireNewIdentifiers)
        {
            if (requireNewIdentifiers)
            {
                RuleFor(supplement => supplement.Id)
                    .Null()
                    .WithErrorCode("meal_plan_create_id_forbidden");
            }
            else
            {
                RuleFor(supplement => supplement.Id)
                    .Must(id => !id.HasValue || id.Value != Guid.Empty)
                    .WithErrorCode("meal_plan_supplement_id_invalid");
            }

            RuleFor(supplement => supplement.SupplementId)
                .NotEmpty()
                .WithErrorCode("meal_plan_supplement_id_required");
            RuleFor(supplement => supplement.Notes)
                .MaximumLength(500)
                .WithErrorCode("meal_plan_supplement_notes_too_long");
            RuleFor(supplement => supplement.Quantity)
                .GreaterThan(0m)
                .WithErrorCode("meal_plan_supplement_quantity_invalid");
            RuleFor(supplement => supplement.OrderNumber)
                .GreaterThan(0)
                .WithErrorCode("meal_plan_supplement_order_invalid");
        }
    }
}
