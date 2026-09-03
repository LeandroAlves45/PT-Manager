using Application.Features.Training.TrainingPlans;
using Application.Features.Training.TrainingPlans.Dtos;

namespace Api.Contracts.Training;

/// <summary>Série prescrita. Identificador nulo cria; presente reconcilia.</summary>
public sealed record ExerciseSetRequest(
    Guid? Id,
    int SetNumber,
    int? PlannedReps,
    decimal? PlannedWeightKg,
    int? RestSecondsMin,
    int? RestSecondsMax);

/// <summary>Exercício prescrito num dia de treino.</summary>
public sealed record DayExerciseRequest(
    Guid? Id,
    Guid ExerciseId,
    int OrderNumber,
    Guid? ExerciseGroupId,
    int? GroupPosition,
    string? Notes,
    IReadOnlyList<ExerciseSetRequest> Sets);

/// <summary>Dia de treino dentro de uma semana do plano.</summary>
public sealed record TrainingDayRequest(
    Guid? Id,
    int DayOfWeek,
    int WeekNumber,
    string? Notes,
    IReadOnlyList<DayExerciseRequest> Exercises);

/// <summary>Estrutura completa desejada depois da escrita.</summary>
public sealed record TrainingPlanStructureRequest(IReadOnlyList<TrainingDayRequest> Days)
{
    /// <summary>Converte a estrutura do contrato na entrada da Application.</summary>
    public TrainingPlanStructureInput ToInput()
    {
        ArgumentNullException.ThrowIfNull(Days);

        return new TrainingPlanStructureInput(
            Days.Select(day => new TrainingPlanStructureInput.TrainingDayInput(
                day.Id,
                day.DayOfWeek,
                day.WeekNumber,
                day.Notes,
                day.Exercises
                    .Select(exercise => new TrainingPlanStructureInput.DayExerciseInput(
                        exercise.Id,
                        exercise.ExerciseId,
                        exercise.OrderNumber,
                        exercise.ExerciseGroupId,
                        exercise.GroupPosition,
                        exercise.Notes,
                        exercise.Sets
                            .Select(set => new TrainingPlanStructureInput.ExerciseSetInput(
                                set.Id,
                                set.SetNumber,
                                set.PlannedReps,
                                set.PlannedWeightKg,
                                set.RestSecondsMin,
                                set.RestSecondsMax))
                            .ToArray()))
                    .ToArray()))
                .ToArray());
    }
}

/// <summary>Cria um plano de treino e a respetiva estrutura inicial.</summary>
public sealed record CreateTrainingPlanRequest(
    Guid ClientId,
    string Name,
    string? Description,
    string? TrainingModality,
    string? Notes,
    DateOnly StartDate,
    DateOnly? EndDate,
    TrainingPlanStructureRequest Structure);

/// <summary>Substitui cabeçalho e estrutura de um plano existente.</summary>
public sealed record ReplaceTrainingPlanRequest(
    string Name,
    string? Description,
    string? TrainingModality,
    string? Notes,
    DateOnly StartDate,
    DateOnly? EndDate,
    TrainingPlanStructureRequest Structure);

/// <summary>Atualiza apenas o cabeçalho, preservando a estrutura.</summary>
public sealed record UpdateTrainingPlanMetadataRequest(
    string Name,
    string? Description,
    string? TrainingModality,
    string? Notes,
    DateOnly StartDate,
    DateOnly? EndDate);

/// <summary>Atualiza apenas a estrutura, preservando o cabeçalho.</summary>
public sealed record UpdateTrainingPlanStructureRequest(
    TrainingPlanStructureRequest Structure);

/// <summary>Série prescrita, tal como devolvida.</summary>
public sealed record ExerciseSetResponse(
    Guid Id,
    int SetNumber,
    int? PlannedReps,
    decimal? PlannedWeightKg,
    int? RestSecondsMin,
    int? RestSecondsMax)
{
    /// <summary>Projeta a série da Application.</summary>
    public static ExerciseSetResponse From(TrainingPlanDetailsDto.ExerciseSetDto set)
    {
        ArgumentNullException.ThrowIfNull(set);

        return new(
            set.Id,
            set.SetNumber,
            set.PlannedReps,
            set.PlannedWeightKg,
            set.RestSecondsMin,
            set.RestSecondsMax);
    }
}

/// <summary>Exercício prescrito num dia, com as suas séries.</summary>
public sealed record DayExerciseResponse(
    Guid Id,
    Guid ExerciseId,
    string ExerciseName,
    int OrderNumber,
    Guid? ExerciseGroupId,
    int? GroupPosition,
    string? Notes,
    IReadOnlyList<ExerciseSetResponse> Sets)
{
    /// <summary>Projeta a prescrição da Application.</summary>
    public static DayExerciseResponse From(TrainingPlanDetailsDto.DayExerciseDto exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        return new(
            exercise.Id,
            exercise.ExerciseId,
            exercise.ExerciseName,
            exercise.OrderNumber,
            exercise.ExerciseGroupId,
            exercise.GroupPosition,
            exercise.Notes,
            exercise.Sets.Select(ExerciseSetResponse.From).ToArray());
    }
}

/// <summary>Dia de treino com os exercícios ordenados.</summary>
public sealed record TrainingDayResponse(
    Guid Id,
    int DayOfWeek,
    int WeekNumber,
    string? Notes,
    IReadOnlyList<DayExerciseResponse> Exercises)
{
    /// <summary>Projeta o dia da Application.</summary>
    public static TrainingDayResponse From(TrainingPlanDetailsDto.TrainingDayDto day)
    {
        ArgumentNullException.ThrowIfNull(day);

        return new(
            day.Id,
            day.DayOfWeek,
            day.WeekNumber,
            day.Notes,
            day.Exercises.Select(DayExerciseResponse.From).ToArray());
    }
}

/// <summary>Plano de treino completo, na perspetiva do personal trainer.</summary>
public sealed record TrainingPlanDetailsResponse(
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
    bool NeedsReview,
    IReadOnlyList<TrainingDayResponse> Days,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>Projeta o detalhe da Application.</summary>
    public static TrainingPlanDetailsResponse From(TrainingPlanDetailsDto plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new(
            plan.Id,
            plan.ClientId,
            plan.Name,
            plan.Description,
            plan.TrainingModality,
            plan.Notes,
            plan.StartDate,
            plan.EndDate,
            plan.IsActive,
            plan.IsArchived,
            plan.NeedsReview,
            plan.Days.Select(TrainingDayResponse.From).ToArray(),
            plan.CreatedAt,
            plan.UpdatedAt);
    }
}

/// <summary>Resumo de plano de treino para listagens.</summary>
public sealed record TrainingPlanSummaryResponse(
    Guid Id,
    Guid ClientId,
    string Name,
    string? Description,
    string? TrainingModality,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsActive,
    bool IsArchived,
    bool NeedsReview,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    /// <summary>Projeta o resumo da Application.</summary>
    public static TrainingPlanSummaryResponse From(TrainingPlanSummaryDto plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new(
            plan.Id,
            plan.ClientId,
            plan.Name,
            plan.Description,
            plan.TrainingModality,
            plan.StartDate,
            plan.EndDate,
            plan.IsActive,
            plan.IsArchived,
            plan.NeedsReview,
            plan.CreatedAt,
            plan.UpdatedAt);
    }
}
