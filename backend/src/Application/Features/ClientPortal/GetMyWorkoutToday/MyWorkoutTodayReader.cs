using Application.Features.ClientPortal.Abstractions;
using Application.Features.ClientPortal.Dtos;
using Domain.Services;

namespace Application.Features.ClientPortal.GetMyWorkoutToday;

/// <summary>
/// Monta o treino de hoje a partir do plano ativo, do calendário cíclico e dos
/// registos do dia. É partilhado pelo endpoint dedicado e pela home do portal, para que os
/// dois mostrem exatamente o mesmo sem duplicar regras.
/// </summary>
public sealed class MyWorkoutTodayReader
{
    private readonly IMyTrainingPlanQueries _planQueries;
    private readonly IMyWorkoutTodayQueries _queries;

    public MyWorkoutTodayReader(IMyTrainingPlanQueries planQueries, IMyWorkoutTodayQueries queries)
    {
        _planQueries = planQueries ?? throw new ArgumentNullException(nameof(planQueries));
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
    }

    /// <summary>
    /// Devolve o treino de hoje ou null quando o cliente não tem plano ativo visível.
    /// </summary>
    public async Task<MyWorkoutTodayDto?> ReadAsync(
        Guid trainerId,
        Guid clientUserId,
        DateOnly localDate,
        DateTimeOffset dayRangeStartUtc,
        DateTimeOffset dayRangeEndUtc,
        CancellationToken cancellationToken)
    {
        var plan = await _planQueries.GetActiveAsync(trainerId, clientUserId, cancellationToken);
        if (plan is null)
            return null;

        var cycleLength = plan.Days.Count == 0 ? 1 : plan.Days.Max(day => day.WeekNumber);
        var slot = TrainingPlanSchedule.Resolve(plan.StartDate, plan.EndDate, cycleLength, localDate);

        if (slot is null)
            return new MyWorkoutTodayDto(
                localDate,
                MyWorkoutTodayStatus.OutsidePlan,
                plan.Id,
                plan.Name,
                null,
                null,
                null,
                EmptyProgress,
                null,
                NextWorkout(plan, cycleLength, localDate));

        var day = plan.Days.FirstOrDefault(item =>
            item.WeekNumber == slot.Value.WeekNumber && item.DayOfWeek == slot.Value.DayOfWeek);

        if (day is null)
            return new MyWorkoutTodayDto(
                localDate,
                MyWorkoutTodayStatus.Rest,
                plan.Id,
                plan.Name,
                slot.Value.WeekNumber,
                slot.Value.DayOfWeek,
                null,
                EmptyProgress,
                null,
                NextWorkout(plan, cycleLength, localDate));

        var logs = await _queries.ListMyLogsAsync(
            trainerId,
            clientUserId,
            plan.Id,
            dayRangeStartUtc,
            dayRangeEndUtc,
            cancellationToken);

        var completedAt = await _queries.GetMyCompletionAsync(
            trainerId,
            clientUserId,
            day.Id,
            localDate,
            cancellationToken);

        // Um par (prescrição, série) pode ter mais do que um registo histórico no mesmo dia:
        // mostra-se o mais recente, que é o que o cliente vê como estado atual da linha.
        var logBySet = logs
            .GroupBy(log => (log.PrescriptionId, log.SetNumber))
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(log => log.PerformedAt).First());

        var exercises = day.Exercises
            .Select(exercise =>
            {
                var sets = exercise.Sets
                    .Select(set =>
                    {
                        logBySet.TryGetValue((exercise.Id, set.SetNumber), out var log);
                        return new MyWorkoutTodayDto.SetDto(
                            set.Id,
                            set.SetNumber,
                            set.PlannedReps,
                            set.PlannedWeightKg,
                            set.RestSecondsMin,
                            set.RestSecondsMax,
                            set.PlannedRpe,
                            log is null
                                ? null
                                : new MyWorkoutTodayDto.LoggedSetDto(
                                    log.LogId,
                                    log.WeightKg,
                                    log.RepsDone,
                                    log.Rpe,
                                    log.PerformedAt));
                    })
                    .ToList();

                return new MyWorkoutTodayDto.ExerciseDto(
                    exercise.Id,
                    exercise.OrderNumber,
                    exercise.ExerciseName,
                    exercise.IsUnavailable,
                    exercise.ExerciseGroupId,
                    exercise.GroupPosition,
                    exercise.Notes,
                    sets.Count > 0 && sets.TrueForAll(set => set.Logged is not null),
                    sets);
            })
            .ToList();

        var progress = new MyWorkoutTodayDto.ProgressDto(
            exercises.Sum(exercise => exercise.Sets.Count),
            exercises.Sum(exercise => exercise.Sets.Count(set => set.Logged is not null)),
            exercises.Count,
            exercises.Count(exercise => exercise.IsCompleted));

        return new MyWorkoutTodayDto(
            localDate,
            MyWorkoutTodayStatus.Workout,
            plan.Id,
            plan.Name,
            slot.Value.WeekNumber,
            slot.Value.DayOfWeek,
            new MyWorkoutTodayDto.DayDto(day.Id, day.Notes, exercises),
            progress,
            completedAt,
            null);
    }

    private static MyWorkoutTodayDto.ProgressDto EmptyProgress => new(0, 0, 0, 0);

    private static MyWorkoutTodayDto.NextWorkoutDto? NextWorkout(
        MyTrainingPlanDto plan,
        int cycleLength,
        DateOnly localDate)
    {
        var slots = plan.Days
            .Select(day => new ScheduledSlot(day.WeekNumber, day.DayOfWeek))
            .ToHashSet();

        var next = TrainingPlanSchedule.FindNextScheduledDate(
            plan.StartDate,
            plan.EndDate,
            cycleLength,
            localDate,
            slots);

        if (next is null)
            return null;

        var slot = TrainingPlanSchedule.Resolve(
            plan.StartDate,
            plan.EndDate,
            cycleLength,
            next.Value);

        return slot is null
            ? null
            : new MyWorkoutTodayDto.NextWorkoutDto(
                next.Value,
                slot.Value.WeekNumber,
                slot.Value.DayOfWeek);
    }
}
