namespace Application.Features.ClientPortal.Dtos;

/// <summary>
/// Resumo dos quatro cartões da home do portal num só pedido. Cada cartão é o
/// mínimo para o cartão do ecrã; o detalhe continua nos endpoints dedicados.
/// <c>InitialAssessmentPending</c> mostra o cartão da avaliação inicial no topo, até ser gravada.
/// <c>AccessNotice</c> existe só durante os dias de acesso de um personal trainer bloqueado e
/// fica antes desse cartão.
/// </summary>
public sealed record MyPortalHomeDto(
    DateOnly LocalDate,
    MyPortalHomeDto.WorkoutCardDto? Workout,
    MyPortalHomeDto.NutritionCardDto? Nutrition,
    int ActiveMealPlanCount,
    MyPortalHomeDto.SupplementsCardDto Supplements,
    MyPortalHomeDto.NextCheckInCardDto? NextCheckIn,
    bool InitialAssessmentPending,
    MyPortalHomeDto.AccessNoticeDto? AccessNotice)
{
    /// <summary>
    /// Aviso do fim do acesso (RN-013): o instante em UTC e o fuso do personal trainer em que
    /// se mostra o dia e a hora. Nunca traz o motivo nem a nota.
    /// </summary>
    public sealed record AccessNoticeDto(DateTime EndsAt, string TimeZone);

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

    /// <summary>
    /// Cartão "Plano Alimentar": o plano escolhido. Null sem escolha; com
    /// <c>ActiveMealPlanCount</c> maior que zero o cartão convida a escolher um plano.
    /// </summary>
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
