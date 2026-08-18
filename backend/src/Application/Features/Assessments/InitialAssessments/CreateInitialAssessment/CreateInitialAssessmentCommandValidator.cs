using Domain.Exceptions;
using Domain.ValueObjects;
using FluentValidation;

namespace Application.Features.Assessments.InitialAssessments.CreateInitialAssessment;

/// <summary>Valida a criação de uma avaliação inicial.</summary>
public sealed class CreateInitialAssessmentCommandValidator
    : AbstractValidator<CreateInitialAssessmentCommand>
{
    public CreateInitialAssessmentCommandValidator()
    {
        RuleFor(command => command.ClientId)
            .NotEmpty()
            .WithErrorCode("client_id_required");

        RuleFor(command => command.WeightKg)
            .GreaterThan(0).WithErrorCode("weight_invalid");

        RuleFor(command => command.HeightCm)
            .GreaterThan(0).WithErrorCode("height_invalid");

        RuleFor(command => command.BodyFatPercentage)
            .Must(value => !value.HasValue || value.Value is > 0 and < 100)
            .WithErrorCode("body_fat_percentage_invalid");

        RuleFor(command => command.FitnessLevel)
            .NotEmpty()
            .WithErrorCode("fitness_level_invalid")
            .MaximumLength(50)
            .WithErrorCode("fitness_level_invalid");

        RuleFor(command => command.ActivityLevel)
            .Must(value => TryActivityLevel(value))
            .WithErrorCode("activity_level_invalid");

        RuleFor(command => command.Goals)
            .NotEmpty()
            .WithErrorCode("goals_required");

        RuleFor(command => command.Profession)
            .MaximumLength(255)
            .WithErrorCode("profession_too_long");

        RuleFor(command => command.BodyMeasurements!)
            .SetValidator(new BodyMeasurementsInputValidator())
            .When(command => command.BodyMeasurements is not null);

        RuleFor(command => command.NutritionIntake!)
            .SetValidator(new NutritionIntakeInputValidator())
            .When(command => command.NutritionIntake is not null);
    }

    private static bool TryActivityLevel(string? value)
    {
        try
        {
            ActivityLevel.FromString(value?.Trim() ?? string.Empty);
            return true;
        }
        catch (DomainException)
        {
            return false;
        }
    }
}
