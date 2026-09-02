namespace Api.FunctionalTests.Support;

/// <summary>Payloads HTTP em snake_case para os testes funcionais de Nutrition.</summary>
internal static class NutritionHttpPayloads
{
    internal static object ManualEnergyCalculation() => new
    {
        calculation_origin = "manual_energy",
        energy_formula = (string?)null,
        weight_kg = 80m,
        height_cm = (decimal?)null,
        age = (int?)null,
        sex = (string?)null,
        body_fat_percentage = (decimal?)null,
        activity_level = (string?)null,
        goal_type = (string?)null,
        goal_adjustment_kcal = (decimal?)null,
        manual_target_kcal = 2000m,
        macro_mode = "manual_grams",
        protein_percentage = (decimal?)null,
        carbs_percentage = (decimal?)null,
        fats_percentage = (decimal?)null,
        protein_grams_per_kg = (decimal?)null,
        fats_grams_per_kg = (decimal?)null,
        protein_grams = 150m,
        carbs_grams = 200m,
        fats_grams = 66.67m
    };

    internal static object GramsPerKgPreviewCalculation() => new
    {
        calculation_origin = "manual_energy",
        energy_formula = (string?)null,
        weight_kg = 80m,
        height_cm = (decimal?)null,
        age = (int?)null,
        sex = (string?)null,
        body_fat_percentage = (decimal?)null,
        activity_level = (string?)null,
        goal_type = (string?)null,
        goal_adjustment_kcal = (decimal?)null,
        manual_target_kcal = 2400m,
        macro_mode = "grams_per_kg",
        protein_percentage = (decimal?)null,
        carbs_percentage = (decimal?)null,
        fats_percentage = (decimal?)null,
        protein_grams_per_kg = 2m,
        fats_grams_per_kg = 1m,
        protein_grams = (decimal?)null,
        carbs_grams = (decimal?)null,
        fats_grams = (decimal?)null
    };

    internal static object CreateMealPlan(
        Guid clientId,
        Guid foodId,
        Guid supplementId,
        int mealCount = 2) => new
        {
            client_id = clientId,
            name = "Plano funcional",
            description = (string?)null,
            starts_date = "2026-08-10",
            ends_date = (string?)null,
            calculation = ManualEnergyCalculation(),
            structure = new
            {
                meals = Enumerable.Range(1, mealCount)
                .Select(order => new
                {
                    id = (Guid?)null,
                    meal_type = $"Refeição {order}",
                    order_number = order,
                    items = new[]
                    {
                        new
                        {
                            id = (Guid?)null,
                            food_id = foodId,
                            quantity_in_grams = 100m + order,
                            order_number = 1
                        }
                    },
                    supplements = new[]
                    {
                        new
                        {
                            id = (Guid?)null,
                            supplement_id = supplementId,
                            notes = (string?)null,
                            quantity = 5m,
                            order_number = 1
                        }
                    }
                })
                .ToArray()
            }
        };

    internal static object UpdateMealPlan(
        Guid foodId,
        Guid supplementId,
        IReadOnlyList<object> meals,
        string name = "Plano actualizado") => new
        {
            name,
            description = (string?)null,
            starts_date = "2026-08-10",
            ends_date = (string?)null,
            calculation = (object?)null,
            structure = new { meals }
        };
}
