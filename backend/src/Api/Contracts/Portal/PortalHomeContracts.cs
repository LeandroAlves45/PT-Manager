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

/// <summary>
/// Home do portal do cliente. <c>nutrition</c> é o plano escolhido; sem escolha vem null e
/// <c>active_meal_plan_count</c> diz se há planos para escolher.
/// </summary>
public sealed record MyPortalHomeResponse(
    DateOnly LocalDate,
    MyHomeWorkoutResponse? Workout,
    MyHomeNutritionResponse? Nutrition,
    int ActiveMealPlanCount,
    MyHomeSupplementsResponse Supplements,
    MyNextCheckInResponse? NextCheckIn,
    bool InitialAssessmentPending,
    MyAccessNoticeResponse? AccessNotice)
{
    /// <summary>Converte o DTO da Application.</summary>
    public static MyPortalHomeResponse From(MyPortalHomeDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new MyPortalHomeResponse(
            dto.LocalDate,
            dto.Workout is null ? null : MyHomeWorkoutResponse.From(dto.Workout),
            dto.Nutrition is null ? null : MyHomeNutritionResponse.From(dto.Nutrition),
            dto.ActiveMealPlanCount,
            MyHomeSupplementsResponse.From(dto.Supplements),
            dto.NextCheckIn is null
                ? null
                : new MyNextCheckInResponse(
                    dto.NextCheckIn.Id, dto.NextCheckIn.CheckInDate, dto.NextCheckIn.IsToday),
            dto.InitialAssessmentPending,
            dto.AccessNotice is null
                ? null
                : new MyAccessNoticeResponse(dto.AccessNotice.EndsAt, dto.AccessNotice.TimeZone));
    }
}

/// <summary>
/// Aviso de que o acesso ao portal acaba: o instante em UTC e o fuso do personal trainer em
/// que o dia e a hora se mostram. Só existe durante os dias de acesso.
/// </summary>
public sealed record MyAccessNoticeResponse(DateTime EndsAt, string TimeZone);
