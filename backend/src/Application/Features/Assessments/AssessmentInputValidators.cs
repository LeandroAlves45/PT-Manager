using FluentValidation;

namespace Application.Features.Assessments;

/// <summary>Valida medidas corporais opcionais antes de construir o value object.</summary>
internal sealed class BodyMeasurementsInputValidator
    : AbstractValidator<AssessmentValueInput.BodyMeasurements>
{
    public BodyMeasurementsInputValidator()
    {
        RuleForEach(x => new decimal?[]
            {
                x.WaistCm,
                x.HipCm,
                x.ChestCm,
                x.RightArmCm,
                x.LeftArmCm,
                x.RightThighCm,
                x.LeftThighCm,
                x.RightCalfCm,
                x.LeftCalfCm
            })
            .Must(value => !value.HasValue || value.Value > 0)
            .WithErrorCode("body_measurement_invalid");
    }
}

/// <summary>Valida hábitos e textos da avaliação inicial.</summary>
internal sealed class NutritionIntakeInputValidator
    : AbstractValidator<AssessmentValueInput.NutritionIntake>
{
    public NutritionIntakeInputValidator()
    {
        RuleForEach(x => new[]
            {
                x.FoodPreferences,
                x.DislikedFoods,
                x.FoodIntolerances,
                x.FoodAllergies,
                x.DietaryRestrictions,
                x.DailyRoutine,
                x.HungriestTimeOfDay,
                x.CurrentSupplements,
                x.OtherNotes
            })
            .MaximumLength(2000)
            .WithErrorCode("nutrition_text_too_long");

        RuleFor(x => x.SleepQuality)
            .InclusiveBetween(1, 5)
            .When(x => x.SleepQuality.HasValue)
            .WithErrorCode("sleep_quality_invalid");

        RuleFor(x => x.Mood)
            .InclusiveBetween(1, 5)
            .When(x => x.Mood.HasValue)
            .WithErrorCode("mood_invalid");

        RuleFor(x => x.StressLevel)
            .InclusiveBetween(1, 5)
            .When(x => x.StressLevel.HasValue)
            .WithErrorCode("stress_level_invalid");

        RuleFor(x => x.AvgWaterLitersPerDay)
            .GreaterThan(0)
            .When(x => x.AvgWaterLitersPerDay.HasValue)
            .WithErrorCode("average_water_invalid");
    }
}

/// <summary>Valida os campos qualitativos opcionais de CheckIn.</summary>
internal sealed class CheckInFeedbackInputValidator
    : AbstractValidator<AssessmentValueInput.CheckInFeedback>
{
    public CheckInFeedbackInputValidator()
    {
        RuleForEach(x => new[]
            {
                x.Appetite,
                x.Digestion,
                x.TrainingLoad,
                x.RecoverySleep,
                x.EnergyLevels,
                x.BodyResponse
            })
            .MaximumLength(2000)
            .WithErrorCode("check_in_feedback_too_long");
    }
}
