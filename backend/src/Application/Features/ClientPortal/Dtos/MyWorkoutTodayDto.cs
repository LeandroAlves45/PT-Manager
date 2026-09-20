namespace Application.Features.ClientPortal.Dtos;

/// <summary>Estado do dia no calendário do plano ativo.</summary>
public enum MyWorkoutTodayStatus
{
    Workout,
    Rest,
    OutsidePlan
}

/// <summary>
/// Treino de hoje do cliente autenticado, Sem client_id nem campos internos: o portal
/// só devolve o que o próprio cliente precisa de ver.
/// </summary>
public sealed record MyWorkoutTodayDto(
    DateOnly LocalDate,
    MyWorkoutTodayStatus Status,
    Guid PlanId,
    string PlanName,
    int? WeekNumber,
    int? DayOfWeek,
    MyWorkoutTodayDto.DayDto? Day,
    MyWorkoutTodayDto.ProgressDto Progress,
    DateTime? CompletedAt,
    MyWorkoutTodayDto.NextWorkoutDto? NextWorkout)
{
    /// <summary>Dia de treino com a prescrição e o que já foi registado.</summary>
    public sealed record DayDto(
        Guid Id,
        string? Notes,
        IReadOnlyList<ExerciseDto> Exercises);

    /// <summary>Exercício prescrito; o Id é o da prescrição usada para registar séries.</summary>
    public sealed record ExerciseDto(
        Guid Id,
        int OrderNumber,
        string ExerciseName,
        bool IsUnavailable,
        Guid? ExerciseGroupId,
        int? GroupPosition,
        string? Notes,
        bool IsCompleted,
        IReadOnlyList<SetDto> Sets);

    /// <summary>Série prescrita e, quando existem o registo de hoje.</summary>
    public sealed record SetDto(
        Guid Id,
        int SetNumber,
        int? PlannedReps,
        decimal? PlannedWeightKg,
        int? RestSecondsMin,
        int? RestSecondsMax,
        decimal? PlannedRpe,
        LoggedSetDto? Logged);

    /// <summary>Registo de hoje de uma série.</summary>
    public sealed record LoggedSetDto(
        Guid LogId,
        decimal WeightKg,
        int RepsDone,
        decimal? Rpe,
        DateTimeOffset PerformedAt);

    /// <summary>Progresso do dia, em séries e em exercícios.</summary>
    public sealed record ProgressDto(
        int PlannedSets,
        int LoggedSets,
        int PlannedExercises,
        int CompletedExercises);

    /// <summary>Próximo dia com treino no calendário cíclico.</summary>
    public sealed record NextWorkoutDto(DateOnly Date, int WeekNumber, int DayOfWeek);

}
