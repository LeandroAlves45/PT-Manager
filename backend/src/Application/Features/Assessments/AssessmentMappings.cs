using Application.Features.Assessments.CheckIns.Dtos;
using Application.Features.Assessments.InitialAssessments.Dtos;
using Domain.Entities.Assessments;
using Domain.ValueObjects;

namespace Application.Features.Assessments;

/// <summary>Converte entidades Assessment em contratos de Application.</summary>
public static class AssessmentMappings
{
    public static InitialAssessmentDto ToDto(this InitialAssessment assessment) => new(
        assessment.Id,
        assessment.ClientId,
        assessment.WeightKg,
        assessment.HeightCm,
        assessment.BodyFatPercentage,
        assessment.MedicalConditions,
        assessment.FitnessLevel,
        assessment.ActivityLevel.Value,
        assessment.Goals,
        assessment.Profession,
        assessment.BodyMeasurements.ToDto(),
        assessment.NutritionIntake.ToDto(),
        assessment.CreatedAt,
        assessment.UpdatedAt
    );

    public static CheckInDto ToDto(this CheckIn checkIn, DateOnly localToday) => new(
        checkIn.Id,
        checkIn.ClientId,
        checkIn.CheckInDate,
        checkIn.TargetDate,
        checkIn.WeightKg,
        checkIn.BodyFatPercentage,
        checkIn.Notes,
        checkIn.BodyMeasurements.ToDto(),
        checkIn.Feedback.ToDto(),
        checkIn.TrainingAdherenceScore,
        checkIn.NutritionAdherenceScore,
        GetStatus(checkIn, localToday),
        checkIn.RespondedAt,
        checkIn.CancelledAt,
        checkIn.CreatedAt,
        checkIn.UpdatedAt
    );

    public static AssessmentValueDto.BodyMeasurements ToDto(this BodyMeasurements value) => new(
        value.WaistCm,
        value.HipCm,
        value.ChestCm,
        value.RightArmCm,
        value.LeftArmCm,
        value.RightThighCm,
        value.LeftThighCm,
        value.RightCalfCm,
        value.LeftCalfCm
    );

    public static AssessmentValueDto.NutritionIntake ToDto(this NutritionIntake value) => new(
        value.FoodPreferences,
        value.DislikedFoods,
        value.FoodIntolerances,
        value.FoodAllergies,
        value.DietaryRestrictions,
        value.DailyRoutine,
        value.SleepQuality,
        value.Mood,
        value.StressLevel,
        value.AvgWaterLitersPerDay,
        value.HungriestTimeOfDay,
        value.UsesSupplements,
        value.CurrentSupplements,
        value.OtherNotes
    );

    public static AssessmentValueDto.CheckInFeedback ToDto(this CheckInFeedback value) => new(
        value.Appetite,
        value.Digestion,
        value.TrainingLoad,
        value.RecoverySleep,
        value.EnergyLevels,
        value.BodyResponse
    );

    private static string GetStatus(CheckIn checkIn, DateOnly localToday)
    {
        if (checkIn.CancelledAt.HasValue)
            return "cancelled";

        if (checkIn.RespondedAt.HasValue)
            return "answered";

        return checkIn.CheckInDate < localToday ? "missed" : "scheduled";
    }
}
