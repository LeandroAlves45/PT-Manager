using Domain.ValueObjects;

namespace Application.Features.Assessments;

/// <summary>Converte inputs de avaliação em value objects validados.</summary>
internal static class AssessmentValueMappings
{
    internal static BodyMeasurements ToDomain(this AssessmentValueInput.BodyMeasurements? input) =>
        input is null ? BodyMeasurements.Empty : new BodyMeasurements(
            input.WaistCm,
            input.HipCm,
            input.ChestCm,
            input.RightArmCm,
            input.LeftArmCm,
            input.RightThighCm,
            input.LeftThighCm,
            input.RightCalfCm,
            input.LeftCalfCm
        );

    internal static NutritionIntake ToDomain(
        this AssessmentValueInput.NutritionIntake? input
    ) =>
        input is null ? NutritionIntake.Empty : new NutritionIntake(
            input.FoodPreferences,
            input.DislikedFoods,
            input.FoodIntolerances,
            input.FoodAllergies,
            input.DietaryRestrictions,
            input.DailyRoutine,
            input.SleepQuality,
            input.Mood,
            input.StressLevel,
            input.AvgWaterLitersPerDay,
            input.HungriestTimeOfDay,
            input.UsesSupplements,
            input.CurrentSupplements,
            input.OtherNotes
        );

    internal static CheckInFeedback ToDomain(
        this AssessmentValueInput.CheckInFeedback? input
    ) =>
        input is null ? CheckInFeedback.Empty : new CheckInFeedback(
            input.Appetite,
            input.Digestion,
            input.TrainingLoad,
            input.RecoverySleep,
            input.EnergyLevels,
            input.BodyResponse
        );
}
