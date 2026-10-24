using Application.Features.ClientPortal.Dtos;
using Application.Features.ClientPortal.ListMyWorkoutDays;

namespace Api.Contracts.Portal;

/// <summary>Registo de uma série nesse dia, visto pelo cliente, com a nota que ele escreveu.</summary>
public sealed record MyLoggedSetResponse(
    Guid LogId,
    decimal WeightKg,
    int RepsDone,
    decimal? Rpe,
    DateTimeOffset PerformedAt,
    string? Notes)
{
    public static MyLoggedSetResponse From(MyWorkoutTodayDto.LoggedSetDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new MyLoggedSetResponse(
            dto.LogId, dto.WeightKg, dto.RepsDone, dto.Rpe, dto.PerformedAt, dto.Notes);
    }
}

/// <summary>Série prescrita para hoje e o respetivo registo, se existir.</summary>
public sealed record MyWorkoutSetResponse(
    Guid Id,
    int SetNumber,
    int? PlannedReps,
    int? PlannedRepsMax,
    decimal? PlannedWeightKg,
    int? RestSecondsMin,
    int? RestSecondsMax,
    decimal? PlannedRpe,
    MyLoggedSetResponse? Logged)
{
    public static MyWorkoutSetResponse From(MyWorkoutTodayDto.SetDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new MyWorkoutSetResponse(
            dto.Id,
            dto.SetNumber,
            dto.PlannedReps,
            dto.PlannedRepsMax,
            dto.PlannedWeightKg,
            dto.RestSecondsMin,
            dto.RestSecondsMax,
            dto.PlannedRpe,
            dto.Logged is null ? null : MyLoggedSetResponse.From(dto.Logged));
    }
}

/// <summary>
/// Exercício do treino de hoje. O <c>exercise_id</c> é o do catálogo, usado para pedir o
/// vídeo quando <c>has_ready_video</c> é verdadeiro.
/// </summary>
public sealed record MyWorkoutExerciseResponse(
    Guid Id,
    Guid ExerciseId,
    int OrderNumber,
    string ExerciseName,
    bool HasReadyVideo,
    Guid? ExerciseGroupId,
    int? GroupPosition,
    string? Notes,
    bool IsCompleted,
    IReadOnlyList<MyWorkoutSetResponse> Sets)
{
    public static MyWorkoutExerciseResponse From(MyWorkoutTodayDto.ExerciseDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new MyWorkoutExerciseResponse(
            dto.Id,
            dto.ExerciseId,
            dto.OrderNumber,
            dto.ExerciseName,
            dto.HasReadyVideo,
            dto.ExerciseGroupId,
            dto.GroupPosition,
            dto.Notes,
            dto.IsCompleted,
            dto.Sets.Select(MyWorkoutSetResponse.From).ToList());
    }
}

/// <summary>Dia de treino de hoje.</summary>
public sealed record MyWorkoutDayResponse(
    Guid Id,
    string? Notes,
    IReadOnlyList<MyWorkoutExerciseResponse> Exercises)
{
    public static MyWorkoutDayResponse From(MyWorkoutTodayDto.DayDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new MyWorkoutDayResponse(
            dto.Id,
            dto.Notes,
            dto.Exercises.Select(MyWorkoutExerciseResponse.From).ToList());
    }
}

/// <summary>Progresso do treino de hoje, em séries e em exercícios.</summary>
public sealed record MyWorkoutProgressResponse(
    int PlannedSets,
    int LoggedSets,
    int PlannedExercises,
    int CompletedExercises)
{
    public static MyWorkoutProgressResponse From(MyWorkoutTodayDto.ProgressDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new MyWorkoutProgressResponse(
            dto.PlannedSets, dto.LoggedSets, dto.PlannedExercises, dto.CompletedExercises);
    }
}

/// <summary>Próximo dia com treino.</summary>
public sealed record MyNextWorkoutResponse(DateOnly Date, int WeekNumber, int DayOfWeek)
{
    public static MyNextWorkoutResponse From(MyWorkoutTodayDto.NextWorkoutDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new MyNextWorkoutResponse(dto.Date, dto.WeekNumber, dto.DayOfWeek);
    }
}

/// <summary>
/// Treino de um dia do cliente autenticado: hoje, para registar, ou um dia passado, só para
/// ler. <c>completion_notes</c> é a nota escrita ao concluir.
/// </summary>
public sealed record MyWorkoutTodayResponse(
    DateOnly LocalDate,
    MyWorkoutTodayStatus Status,
    Guid PlanId,
    string PlanName,
    int? WeekNumber,
    int? DayOfWeek,
    MyWorkoutDayResponse? Day,
    MyWorkoutProgressResponse Progress,
    DateTime? CompletedAt,
    string? CompletionNotes,
    MyNextWorkoutResponse? NextWorkout)
{
    public static MyWorkoutTodayResponse From(MyWorkoutTodayDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new MyWorkoutTodayResponse(
            dto.LocalDate,
            dto.Status,
            dto.PlanId,
            dto.PlanName,
            dto.WeekNumber,
            dto.DayOfWeek,
            dto.Day is null ? null : MyWorkoutDayResponse.From(dto.Day),
            MyWorkoutProgressResponse.From(dto.Progress),
            dto.CompletedAt,
            dto.CompletionNotes,
            dto.NextWorkout is null ? null : MyNextWorkoutResponse.From(dto.NextWorkout));
    }
}

/// <summary>Dia passado com treino do plano ativo, para o filtro de dia do portal.</summary>
public sealed record MyPastWorkoutDayResponse(DateOnly Date, int WeekNumber, int DayOfWeek)
{
    public static MyPastWorkoutDayResponse From(MyPastWorkoutDayDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new MyPastWorkoutDayResponse(dto.Date, dto.WeekNumber, dto.DayOfWeek);
    }
}

/// <summary>Dias passados com treino do plano ativo, do mais recente para o mais antigo.</summary>
public sealed record MyPastWorkoutDaysResponse(IReadOnlyList<MyPastWorkoutDayResponse> Items)
{
    public static MyPastWorkoutDaysResponse From(IReadOnlyList<MyPastWorkoutDayDto> days)
    {
        ArgumentNullException.ThrowIfNull(days);
        return new MyPastWorkoutDaysResponse(days.Select(MyPastWorkoutDayResponse.From).ToList());
    }
}
