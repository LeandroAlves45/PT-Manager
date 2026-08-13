namespace Application.Features.Training.TrainingPlans;

/// <summary>Representa a estrutura final pretendida para um plano de treino.</summary>
public sealed record TrainingPlanStructureInput(
    IReadOnlyList<TrainingPlanStructureInput.TrainingDayInput> Days
)
{
    /// <summary>Dia existente quando o Id tem valor; novo quando é null.</summary>
    public sealed record TrainingDayInput(
        Guid? Id,
        int DayOfWeek,
        int WeekNumber,
        string? Notes,
        IReadOnlyList<DayExerciseInput> Exercises
    );

    /// <summary>Prescrição existente quando o Id tem valor; nova quando é null.</summary>
    public sealed record DayExerciseInput(
        Guid? Id,
        Guid ExerciseId,
        int OrderNumber,
        Guid? ExerciseGroupId,
        int? GroupPosition,
        string? Notes,
        IReadOnlyList<ExerciseSetInput> Sets
    );

    /// <summary>Série existente quando o Id tem valor; nova quando é null.</summary>
    public sealed record ExerciseSetInput(
        Guid? Id,
        int SetNumber,
        int? PlannedReps,
        decimal? PlannedWeightKg,
        int? RestSecondsMin,
        int? RestSecondsMax
    );
}
