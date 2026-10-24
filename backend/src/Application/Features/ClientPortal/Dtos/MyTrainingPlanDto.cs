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
        Guid Id,
        int DayOfWeek,
        int WeekNumber,
        string? Notes,
        IReadOnlyList<ExerciseDto> Exercises);

    /// <summary>
    /// Exercício prescrito; o bloqueado pela moderação e o retirado do plano não chegam aqui. O Id é o da
    /// prescrição (TrainingPlanDayExercise), usado pelo cliente para registar séries;
    /// ExerciseId é o do catálogo, usado para pedir o vídeo. HasReadyVideo só é verdadeiro
    /// quando esse pedido devolveria um URL (vídeo Ready visível e exercício não bloqueado).
    /// </summary>
    public sealed record ExerciseDto(
        Guid Id,
        Guid ExerciseId,
        int OrderNumber,
        string ExerciseName,
        bool HasReadyVideo,
        Guid? ExerciseGroupId,
        int? GroupPosition,
        string? Notes,
        IReadOnlyList<SetDto> Sets);

    /// <summary>Série prescrita; o cliente identifica-a por prescrição e SetNumber.</summary>
    public sealed record SetDto(
        Guid Id,
        int SetNumber,
        int? PlannedReps,
        int? PlannedRepsMax,
        decimal? PlannedWeightKg,
        int? RestSecondsMin,
        int? RestSecondsMax,
        decimal? PlannedRpe);
}
