using Application.Validation;
using Domain.Entities.Assessments;
using Domain.Exceptions;
using FluentValidation;

namespace Application.Features.Assessments.InitialAssessments;

/// <summary>
/// Campos da avaliação inicial, iguais quando é o personal trainer a criá-la ou corrigi-la e
/// quando é o cliente a preenchê-la no portal.
/// </summary>
public interface IInitialAssessmentValues
{
    decimal WeightKg { get; }
    int HeightCm { get; }
    decimal? BodyFatPercentage { get; }
    string? MedicalConditions { get; }
    string FitnessLevel { get; }
    string ActivityLevel { get; }
    string Goals { get; }
    string? Profession { get; }
    AssessmentValueInput.BodyMeasurements? BodyMeasurements { get; }
    AssessmentValueInput.NutritionIntake? NutritionIntake { get; }
}

/// <summary>
/// Regras dos campos da avaliação inicial, partilhadas pelos três comandos que a escrevem. O
/// texto livre recusa o carácter nulo, porque o cliente também o escreve.
/// </summary>
internal sealed class InitialAssessmentValuesValidator<T> : AbstractValidator<T>
    where T : IInitialAssessmentValues
{
    public InitialAssessmentValuesValidator()
    {
        RuleFor(command => command.WeightKg)
            .Must(value => value is > 0 and <= AssessmentLimits.MaxWeightKg &&
                AssessmentLimits.FitsDecimalPlaces(value))
            .WithErrorCode("weight_invalid");

        RuleFor(command => command.HeightCm)
            .GreaterThan(0).WithErrorCode("height_invalid");

        RuleFor(command => command.BodyFatPercentage)
            .Must(value => !value.HasValue ||
                (value.Value is > 0 and < 100 && AssessmentLimits.FitsDecimalPlaces(value.Value)))
            .WithErrorCode("body_fat_percentage_invalid");

        RuleFor(command => command.MedicalConditions)
            .MaximumLength(2000)
            .WithErrorCode("medical_conditions_too_long")
            .WithoutNullCharacter();

        RuleFor(command => command.FitnessLevel)
            .NotEmpty()
            .WithErrorCode("fitness_level_invalid")
            .MaximumLength(50)
            .WithErrorCode("fitness_level_invalid")
            .WithoutNullCharacter();

        RuleFor(command => command.ActivityLevel)
            .Must(TryActivityLevel)
            .WithErrorCode("activity_level_invalid");

        RuleFor(command => command.Goals)
            .NotEmpty()
            .WithErrorCode("goals_required")
            .MaximumLength(2000)
            .WithErrorCode("goals_too_long")
            .WithoutNullCharacter();

        RuleFor(command => command.Profession)
            .MaximumLength(255)
            .WithErrorCode("profession_too_long")
            .WithoutNullCharacter();

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
            Domain.ValueObjects.ActivityLevel.FromString(value?.Trim() ?? string.Empty);
            return true;
        }
        catch (DomainException)
        {
            return false;
        }
    }
}
