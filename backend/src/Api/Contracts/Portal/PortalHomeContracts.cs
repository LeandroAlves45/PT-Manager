using Application.Features.ClientPortal.Dtos;

namespace Api.Contracts.Portal;

/// <summary>Cartão do treino na home do portal.</summary>
public sealed record MyHomeWorkoutResponse(
    MyWorkoutTodayStatus Status,
    int? WeekNumber,
    int? DayOfWeek,
    string? DayNotes,
    int ExerciseCount,
    int PlannedSets,
    int LoggedSets,
    bool IsCompleted,
    MyNextWorkoutResponse? NextWorkout)
{
    public static MyHomeWorkoutResponse From(MyPortalHomeDto.WorkoutCardDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new MyHomeWorkoutResponse(
            dto.Status,
            dto.WeekNumber,
            dto.DayOfWeek,
            dto.DayNotes,
            dto.ExerciseCount,
            dto.PlannedSets,
            dto.LoggedSets,
            dto.IsCompleted,
            dto.NextWorkout is null ? null : MyNextWorkoutResponse.From(dto.NextWorkout));
    }
}

/// <summary>Cartão do plano alimentar na home do portal.</summary>
public sealed record MyHomeNutritionResponse(
    Guid MealPlanId,
    string Name,
    decimal TargetKcal,
    int MealCount)
{
    public static MyHomeNutritionResponse From(MyPortalHomeDto.NutritionCardDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new MyHomeNutritionResponse(
            dto.MealPlanId, dto.Name, dto.TargetKcal, dto.MealCount);
    }
}

/// <summary>Cartão dos suplementos na home do portal.</summary>
public sealed record MyHomeSupplementsResponse(int TakenCount, int TotalCount)
{
    public static MyHomeSupplementsResponse From(MyPortalHomeDto.SupplementsCardDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new MyHomeSupplementsResponse(dto.TakenCount, dto.TotalCount);
    }
}

/// <summary>Home do portal do cliente.</summary>
public sealed record MyPortalHomeResponse(
    DateOnly LocalDate,
    MyHomeWorkoutResponse? Workout,
    MyHomeNutritionResponse? Nutrition,
    MyHomeSupplementsResponse Supplements,
    MyNextCheckInResponse? NextCheckIn)
{
    /// <summary>Converte o DTO da Application.</summary>
    public static MyPortalHomeResponse From(MyPortalHomeDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new MyPortalHomeResponse(
            dto.LocalDate,
            dto.Workout is null ? null : MyHomeWorkoutResponse.From(dto.Workout),
            dto.Nutrition is null ? null : MyHomeNutritionResponse.From(dto.Nutrition),
            MyHomeSupplementsResponse.From(dto.Supplements),
            dto.NextCheckIn is null
                ? null
                : new MyNextCheckInResponse(
                    dto.NextCheckIn.Id, dto.NextCheckIn.CheckInDate, dto.NextCheckIn.IsToday));
    }
}
