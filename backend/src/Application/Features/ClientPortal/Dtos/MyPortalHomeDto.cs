namespace Application.Features.ClientPortal.Dtos;

/// <summary>
/// Resumo dos quatro cartões da home do portal num só pedido. Cada cartão é o
/// mínimo para o cartão do ecrã; o detalhe continua nos endpoints dedicados.
/// </summary>
public sealed record MyPortalHomeDto(
    DateOnly LocalDate,
    MyPortalHomeDto.WorkoutCardDto? Workout,
    MyPortalHomeDto.NutritionCardDto? Nutrition,
    MyPortalHomeDto.SupplementsCardDto Supplements,
    MyPortalHomeDto.NextCheckInCardDto? NextCheckIn)
{
    /// <summary>Cartão "O meu treino de hoje".</summary>
    public sealed record WorkoutCardDto(
        MyWorkoutTodayStatus Status,
        int? WeekNumber,
        int? DayOfWeek,
        string? DayNotes,
        int ExerciseCount,
        int PlannedSets,
        int LoggedSets,
        bool IsCompleted,
        MyWorkoutTodayDto.NextWorkoutDto? NextWorkout);

    /// <summary>Cartão "Plano Alimentar".</summary>
    public sealed record NutritionCardDto(
        Guid MealPlanId,
        string Name,
        decimal TargetKcal,
        int MealCount);

    /// <summary>Cartão "Suplementos": "X de Y tomadas hoje".</summary>
    public sealed record SupplementsCardDto(int TakenCount, int TotalCount);

    /// <summary>Cartão "Check-in".</summary>
    public sealed record NextCheckInCardDto(Guid Id, DateOnly CheckInDate, bool IsToday);
}
