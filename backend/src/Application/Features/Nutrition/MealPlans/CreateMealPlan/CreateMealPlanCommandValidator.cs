using Application.Features.Nutrition.Calculations;
using FluentValidation;

namespace Application.Features.Nutrition.MealPlans.CreateMealPlan;

/// <summary>Valida metadados, cálculo e árvore inicial de um plano alimentar.</summary>
public sealed class CreateMealPlanCommandValidator : AbstractValidator<CreateMealPlanCommand>
{
    public CreateMealPlanCommandValidator()
    {
        RuleFor(command => command.ClientId)
            .NotEmpty()
            .WithErrorCode("meal_plan_client_id_required");

        this.ApplyMetadataRules(
            command => command.Name,
            command => command.StartsDate,
            command => command.EndsDate
        );

        RuleFor(command => command.Calculation)
            .NotNull()
            .SetValidator(new NutritionCalculationInputValidator());

        RuleFor(command => command.Structure)
            .NotNull()
            .SetValidator(new MealPlanStructureInputValidator(requireNewIdentifiers: true));
    }
}
