namespace Application.Features.Training.TrainingPlans.Dtos;

/// <summary>Representa metadados e árvore completa de um plano de treino.</summary>
public sealed record TrainingPlanDetailsDto(
    Guid Id,
    Guid ClientId,
    string Name,
    string? Description,
    string? TrainingModality,
    string? Notes,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsActive,
    bool IsArchived,
    IReadOnlyList<TrainingPlanDetailsDto.TrainingDayDto> Days,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>Dia de uma semana do plano.</summary>
    public sealed record TrainingDayDto(
        Guid Id,
        int DayOfWeek,
        int WeekNumber,
        string? Notes,
        IReadOnlyList<DayExerciseDto> Exercises
    );

    /// <summary>Exercício prescrito e dados correntes do catálogo.</summary>
    public sealed record DayExerciseDto(
        Guid Id,
        Guid ExerciseId,
        string ExerciseName,
        int OrderNumber,
        Guid? ExerciseGroupId,
        int? GroupPosition,
        string? Notes,
        IReadOnlyList<ExerciseSetDto> Sets
    );

    /// <summary>Série e valores planeados.</summary>
    public sealed record ExerciseSetDto(
        Guid Id,
        int SetNumber,
        int? PlannedReps,
        decimal? PlannedWeightKg,
        int? RestSecondsMin,
        int? RestSecondsMax
    );
}
