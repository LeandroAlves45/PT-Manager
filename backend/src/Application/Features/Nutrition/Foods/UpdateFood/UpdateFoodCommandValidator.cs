using FluentValidation;

namespace Application.Features.Nutrition.Foods.UpdateFood;

/// <summary>Valida a atualização completa de um alimento privado.</summary>
public sealed class UpdateFoodCommandValidator : AbstractValidator<UpdateFoodCommand>
{
    public UpdateFoodCommandValidator()
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
            .InclusiveBetween(0m, 100m)
            .WithErrorCode("food_protein_invalid");

        RuleFor(command => command.Carbs)
            .InclusiveBetween(0m, 100m)
            .WithErrorCode("food_carbs_invalid");

        RuleFor(command => command.Fats)
            .InclusiveBetween(0m, 100m)
            .WithErrorCode("food_fats_invalid");

        RuleFor(command => command.Fiber)
            .InclusiveBetween(0m, 100m)
            .When(command => command.Fiber.HasValue)
            .WithErrorCode("food_fiber_invalid");

        RuleFor(command => command)
            .Must(command => command.Protein + command.Carbs + command.Fats <= 100m)
            .WithName("Macros")
            .WithErrorCode("food_macros_total_invalid");
    }
}
