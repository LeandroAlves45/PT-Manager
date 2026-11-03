using Domain.ValueObjects;

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
    /// <summary>
    /// O que o cliente pode receber: uma ligação de vídeo que não seja do YouTube ou do Vimeo
    /// (gravada antes da regra atual) sai a null e nunca chega ao portal.
    /// </summary>
    public MyTrainingPlanDto ForClient() => this with
    {
        Days = Days
            .Select(day => day with
            {
                Exercises = day.Exercises
                    .Select(exercise => exercise with { VideoUrl = ExternalVideoUrl.OrNull(exercise.VideoUrl) })
                    .ToArray(),
            })
            .ToArray(),
    };

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
    /// VideoUrl é a ligação externa (YouTube ou Vimeo) que o cliente abre noutro separador.
    /// </summary>
    public sealed record ExerciseDto(
        Guid Id,
        Guid ExerciseId,
        int OrderNumber,
        string ExerciseName,
        bool HasReadyVideo,
        string? VideoUrl,
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
