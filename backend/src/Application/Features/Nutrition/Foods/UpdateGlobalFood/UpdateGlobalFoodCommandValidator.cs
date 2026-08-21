using FluentValidation;

namespace Application.Features.Nutrition.Foods.UpdateGlobalFood;

/// <summary>Valida identificador, nome e macros do alimento global.</summary>
public sealed class UpdateGlobalFoodCommandValidator : AbstractValidator<UpdateGlobalFoodCommand>
{
    public UpdateGlobalFoodCommandValidator()
    {
        RuleFor(command => command.FoodId)
            .NotEmpty()
            .WithErrorCode("food_id_required");

        RuleFor(command => command.Name)
            .NotEmpty()
            .WithErrorCode("food_name_required")
            .MaximumLength(255)
            .WithErrorCode("food_name_too_long");

        RuleFor(command => command.Protein)
            .InclusiveBetween(0, 100)
            .WithErrorCode("food_protein_out_of_range");

        RuleFor(command => command.Carbs)
            .InclusiveBetween(0, 100)
            .WithErrorCode("food_carbs_out_of_range");

        RuleFor(command => command.Fats)
            .InclusiveBetween(0, 100)
            .WithErrorCode("food_fats_out_of_range");

        RuleFor(command => command)
            .Must(command => command.Protein + command.Carbs + command.Fats <= 100)
            .WithErrorCode("food_macros_exceed_total")
            .WithName("Macros");

        RuleFor(command => command.Fiber)
            .InclusiveBetween(0, 100)
            .When(command => command.Fiber.HasValue)
            .WithErrorCode("food_fiber_out_of_range");
    }
}
