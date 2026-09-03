namespace Application.Features.ClientPortal.Dtos;

/// <summary>Plano de treino ativo, na perspectiva do cliente.</summary>
public sealed record MyTrainingPlanDto(
    Guid Id,
    string Name,
    string? Description,
    string? TrainingModality,
    string? Notes,
    DateOnly StartDate,
    DateOnly? EndDate,
    IReadOnlyList<MyTrainingPlanDto.DayDto> Days,
    DateTime UpdatedAt)
{
    /// <summary>Dia de treino vísivel ao cliente.</summary>
    public sealed record DayDto(
        int DayOfWeek,
        int WeekNumber,
        string? Notes,
        IReadOnlyList<ExerciseDto> Exercises);

    /// <summary>Exercício prescrito. IsUnavailable assinala conteúdo bloqueado.</summary>
    public sealed record ExerciseDto(
        int OrderNumber,
        string ExerciseName,
        bool IsUnavailable,
        Guid? ExerciseGroupId,
        int? GroupPosition,
        string? Notes,
        IReadOnlyList<SetDto> Sets);

    /// <summary>Série prescrita, sem identificadores internos.</summary>
    public sealed record SetDto(
        int SetNumber,
        int? PlannedReps,
        decimal? PlannedWeightKg,
        int? RestSecondsMin,
        int? RestSecondsMax);
}
