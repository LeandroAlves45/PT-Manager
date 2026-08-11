using Application.Features.Nutrition.Calculations;
using FluentValidation;

namespace Application.Features.Nutrition.PreviewNutrition;

/// <summary>Valida o payload completo do preview nutricional.</summary>
public sealed class PreviewNutritionCommandValidator : AbstractValidator<PreviewNutritionCommand>
{
    /// <summary>Exige e valida os inputs escolhidos.</summary>
    public PreviewNutritionCommandValidator()
    {
        RuleFor(command => command.Calculation)
            .NotNull()
            .SetValidator(new NutritionCalculationInputValidator());
    }
}
