using System.Linq.Expressions;
using FluentValidation;

namespace Application.Features.Nutrition.MealPlans;

/// <summary>Disponibiliza regras comuns de metadados de MealPlan.</summary>
public static class MealPlanValidationRules
{
    /// <summary>Aplica nome e intervalo de datas a um validator compatível.</summary>
    public static void ApplyMetadataRules<T>(
        this AbstractValidator<T> validator,
        Expression<Func<T, string>> nameSelector,
        Expression<Func<T, DateOnly>> startsDateSelector,
        Expression<Func<T, DateOnly?>> endsDateSelector
    )
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(nameSelector);
        ArgumentNullException.ThrowIfNull(startsDateSelector);
        ArgumentNullException.ThrowIfNull(endsDateSelector);

        validator.RuleFor(nameSelector)
            .NotEmpty()
            .WithErrorCode("meal_plan_name_required")
            .MaximumLength(255)
            .WithErrorCode("meal_plan_name_too_long");

        var getsStartsDate = startsDateSelector.Compile();
        validator.RuleFor(endsDateSelector)
            .Must((instance, endsDate) =>
                !endsDate.HasValue || endsDate.Value >= getsStartsDate(instance))
            .WithErrorCode("meal_plan_date_order_invalid");
    }
}
