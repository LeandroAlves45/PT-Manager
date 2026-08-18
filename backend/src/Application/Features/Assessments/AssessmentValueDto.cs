namespace Application.Features.Assessments;

/// <summary>Agrupa DTOs complexos partilhados por avaliações.</summary>
public static class AssessmentValueDto
{
    public sealed record BodyMeasurements(
        decimal? WaistCm,
        decimal? HipCm,
        decimal? ChestCm,
        decimal? RightArmCm,
        decimal? LeftArmCm,
        decimal? RightThighCm,
        decimal? LeftThighCm,
        decimal? RightCalfCm,
        decimal? LeftCalfCm
    );

    public sealed record NutritionIntake(
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
        string? OtherNotes
    );

    public sealed record CheckInFeedback(
        string? Appetite,
        string? Digestion,
        string? TrainingLoad,
        string? RecoverySleep,
        string? EnergyLevels,
        string? BodyResponse
    );
}
