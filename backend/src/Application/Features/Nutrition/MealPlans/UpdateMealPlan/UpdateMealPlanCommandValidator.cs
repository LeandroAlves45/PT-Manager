using Application.Features.Nutrition.Calculations;
using FluentValidation;

namespace Application.Features.Nutrition.MealPlans.UpdateMealPlan;

/// <summary>Valida uma reconciliação completa do mesmo plano alimentar.</summary>
public sealed class UpdateMealPlanCommandValidator : AbstractValidator<UpdateMealPlanCommand>
{
    public UpdateMealPlanCommandValidator()
    {
        RuleFor(command => command.MealPlanId)
            .NotEmpty()
            .WithErrorCode("meal_plan_id_required");

        this.ApplyMetadataRules(
            command => command.Name,
            command => command.StartsDate,
            command => command.EndsDate
        );

        When(command => command.Calculation is not null, () =>
        {
            RuleFor(command => command.Calculation!)
                .SetValidator(new NutritionCalculationInputValidator());
        });

        RuleFor(command => command.Structure)
            .NotNull()
            .SetValidator(new MealPlanStructureInputValidator(requireNewIdentifiers: false));
    }
}
