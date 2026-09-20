using Application.Features.ClientPortal.Dtos;

namespace Api.Contracts.Portal;

/// <summary>Registo de hoje de uma série, visto pelo cliente.</summary>
public sealed record MyLoggedSetResponse(
    Guid LogId,
    decimal WeightKg,
    int RepsDone,
    decimal? Rpe,
    DateTimeOffset PerformedAt)
{
    public static MyLoggedSetResponse From(MyWorkoutTodayDto.LoggedSetDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new MyLoggedSetResponse(
            dto.LogId, dto.WeightKg, dto.RepsDone, dto.Rpe, dto.PerformedAt);
    }
}

/// <summary>Série prescrita para hoje e o respetivo registo, se existir.</summary>
public sealed record MyWorkoutSetResponse(
    Guid Id,
    int SetNumber,
    int? PlannedReps,
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
            dto.PlannedWeightKg,
            dto.RestSecondsMin,
            dto.RestSecondsMax,
            dto.PlannedRpe,
            dto.Logged is null ? null : MyLoggedSetResponse.From(dto.Logged));
    }
}

/// <summary>Exercício do treino de hoje.</summary>
public sealed record MyWorkoutExerciseResponse(
    Guid Id,
    int OrderNumber,
    string ExerciseName,
    bool IsUnavailable,
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
            dto.OrderNumber,
            dto.ExerciseName,
            dto.IsUnavailable,
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

/// <summary>Treino de hoje do cliente autenticado.</summary>
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
            dto.NextWorkout is null ? null : MyNextWorkoutResponse.From(dto.NextWorkout));
    }
}

