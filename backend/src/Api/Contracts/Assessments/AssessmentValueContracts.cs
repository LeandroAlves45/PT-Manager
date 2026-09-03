using Application.Features.Assessments;

namespace Api.Contracts.Assessments;

/// <summary>Perímetros corporais em centímetros. Campos omitidos ficam nulos.</summary>
public sealed record BodyMeasurementsPayload(
    decimal? WaistCm,
    decimal? HipCm,
    decimal? ChestCm,
    decimal? RightArmCm,
    decimal? LeftArmCm,
    decimal? RightThighCm,
    decimal? LeftThighCm,
    decimal? RightCalfCm,
    decimal? LeftCalfCm)
{
    /// <summary>Converte o contrato na entrada da Application.</summary>
    public AssessmentValueInput.BodyMeasurements ToInput() =>
        new(
            WaistCm,
            HipCm,
            ChestCm,
            RightArmCm,
            LeftArmCm,
            RightThighCm,
            LeftThighCm,
            RightCalfCm,
            LeftCalfCm);

    /// <summary>Projeta os valores devolvidos pela Application.</summary>
    public static BodyMeasurementsPayload From(AssessmentValueDto.BodyMeasurements measurements)
    {
        ArgumentNullException.ThrowIfNull(measurements);

        return new(
            measurements.WaistCm,
            measurements.HipCm,
            measurements.ChestCm,
            measurements.RightArmCm,
            measurements.LeftArmCm,
            measurements.RightThighCm,
            measurements.LeftThighCm,
            measurements.RightCalfCm,
            measurements.LeftCalfCm);
    }
}

/// <summary>Hábitos alimentares e de rotina recolhidos na avaliação inicial.</summary>
public sealed record NutritionIntakePayload(
    string? FoodPreferences,
    string? DislikedFoods,
    string? FoodIntolerances,
    string? FoodAllergies,
    string? DietaryRestrictions,
    string? DailyRoutine,
    int? SleepQuality,
    int? Mood,
    int? StressLevel,
    decimal? AvgWaterLitersPerDay,
    string? HungriestTimeOfDay,
    bool? UsesSupplements,
    string? CurrentSupplements,
    string? OtherNotes)
{
    /// <summary>Converte o contrato na entrada da Application.</summary>
    public AssessmentValueInput.NutritionIntake ToInput() =>
        new(
            FoodPreferences,
            DislikedFoods,
            FoodIntolerances,
            FoodAllergies,
            DietaryRestrictions,
            DailyRoutine,
            SleepQuality,
            Mood,
            StressLevel,
            AvgWaterLitersPerDay,
            HungriestTimeOfDay,
            UsesSupplements,
            CurrentSupplements,
            OtherNotes);

    /// <summary>Projeta os valores devolvidos pela Application.</summary>
    public static NutritionIntakePayload From(AssessmentValueDto.NutritionIntake intake)
    {
        ArgumentNullException.ThrowIfNull(intake);

        return new(
            intake.FoodPreferences,
            intake.DislikedFoods,
            intake.FoodIntolerances,
            intake.FoodAllergies,
            intake.DietaryRestrictions,
            intake.DailyRoutine,
            intake.SleepQuality,
            intake.Mood,
            intake.StressLevel,
            intake.AvgWaterLitersPerDay,
            intake.HungriestTimeOfDay,
            intake.UsesSupplements,
            intake.CurrentSupplements,
            intake.OtherNotes);
    }
}

/// <summary>Respostas qualitativas de um check-in.</summary>
public sealed record CheckInFeedbackPayload(
    string? Appetite,
    string? Digestion,
    string? TrainingLoad,
    string? RecoverySleep,
    string? EnergyLevels,
    string? BodyResponse)
{
    /// <summary>Converte o contrato na entrada da Application.</summary>
    public AssessmentValueInput.CheckInFeedback ToInput() =>
        new(
            Appetite,
            Digestion,
            TrainingLoad,
            RecoverySleep,
            EnergyLevels,
            BodyResponse);

    /// <summary>Projeta os valores devolvidos pela Application.</summary>
    public static CheckInFeedbackPayload From(AssessmentValueDto.CheckInFeedback feedback)
    {
        ArgumentNullException.ThrowIfNull(feedback);

        return new(
            feedback.Appetite,
            feedback.Digestion,
            feedback.TrainingLoad,
            feedback.RecoverySleep,
            feedback.EnergyLevels,
            feedback.BodyResponse);
    }
}
