using Application.Features.Training.TrainingPlans;
using Domain.Entities.Training;
using Infrastructure.Data;

namespace Infrastructure.Persistence.Training;

/// <summary>Aplica operações estruturais ao agregado já bloqueado.</summary>
internal sealed class TrainingPlanStructureCoordinator
{
    private readonly PtManagerDbContext _dbContext;

    public TrainingPlanStructureCoordinator(PtManagerDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public bool ReferenceBelongToAggregate(
        TrainingPlan plan,
        TrainingPlanStructureInput structure
    )
    {
        var days = plan.Days.ToDictionary(day => day.Id);
        foreach (var dayInput in structure.Days.Where(day => day.Id.HasValue))
        {
            if (!days.TryGetValue(dayInput.Id!.Value, out var day))
                return false;

            var exercises = day.Exercises.ToDictionary(item => item.Id);
            foreach (var exerciseInput in dayInput.Exercises.Where(item => item.Id.HasValue))
            {
                if (!exercises.TryGetValue(exerciseInput.Id!.Value, out var exercise))
                    return false;

                var setIds = exercise.Sets.Select(set => set.Id).ToHashSet();
                if (exerciseInput.Sets.Any(set =>
                    set.Id.HasValue && !setIds.Contains(set.Id!.Value)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public bool HasForbiddenHistoricalChanges(
        TrainingPlan plan,
        TrainingPlanStructureInput structure
    )
    {
        // Com histórico não podem existir nós novos, removidos ou substituídos.
        if (structure.Days.Any(day => !day.Id.HasValue) ||
            structure.Days.Count != plan.Days.Count)
        {
            return true;
        }

        foreach (var dayInput in structure.Days)
        {
            var day = plan.GetDay(dayInput.Id!.Value);
            if (day.DayOfWeek != dayInput.DayOfWeek ||
                day.WeekNumber != dayInput.WeekNumber ||
                day.Exercises.Count != dayInput.Exercises.Count ||
                dayInput.Exercises.Any(item => !item.Id.HasValue))
            {
                return true;
            }

            foreach (var exerciseInput in dayInput.Exercises)
            {
                var exercise = day.GetExercise(exerciseInput.Id!.Value);
                if (exercise.ExerciseId != exerciseInput.ExerciseId ||
                    exercise.ExerciseGroupId != exerciseInput.ExerciseGroupId ||
                    exercise.GroupPosition != exerciseInput.GroupPosition ||
                    exercise.Sets.Count != exerciseInput.Sets.Count ||
                    exerciseInput.Sets.Any(set => !set.Id.HasValue))
                {
                    return true;
                }

                foreach (var setInput in exerciseInput.Sets)
                {
                    var set = exercise.GetSet(setInput.Id!.Value);
                    if (set.SetNumber != setInput.SetNumber ||
                        set.PlannedReps != setInput.PlannedReps ||
                        set.PlannedWeightKg != setInput.PlannedWeightKg ||
                        set.RestSecondsMin != setInput.RestSecondsMin ||
                        set.RestSecondsMax != setInput.RestSecondsMax)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public IReadOnlyCollection<Guid> GetChangedExerciseIds(
        TrainingPlan plan,
        TrainingPlanStructureInput structure
    )
    {
        var existing = plan.Days
            .SelectMany(day => day.Exercises)
            .ToDictionary(item => item.Id);

        return structure.Days
            .SelectMany(day => day.Exercises)
            .Where(input => !input.Id.HasValue ||
                existing[input.Id.Value].ExerciseId != input.ExerciseId)
            .Select(input => input.ExerciseId)
            .Distinct()
            .ToArray();
    }

    public void AddNewStructure(
        TrainingPlan plan,
        TrainingPlanStructureInput structure,
        DateTime now
    )
    {
        foreach (var dayInput in structure.Days
            .OrderBy(day => day.WeekNumber)
            .ThenBy(day => day.DayOfWeek))
        {
            var day = plan.AddDay(
                dayInput.DayOfWeek,
                dayInput.WeekNumber,
                dayInput.Notes,
                now);
            AddExercises(day, dayInput.Exercises, now);
        }
    }

    public async Task<bool> PrepareUniqueValuesAsync(
        TrainingPlan plan,
        TrainingPlanStructureInput structure,
        DateTime now,
        CancellationToken cancellationToken = default
    )
    {
        // As remoções libertam primiero posições válidas. Depois, um único slot livre é suficiente
        // para resolver swaps e ciclos sem violar checks.
        RemoveOmittedNodes(plan, structure, now);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (!await ReorderDaysAsync(plan, structure, now, cancellationToken))
            return false;

        foreach (var day in plan.Days)
            StageExerciseOrders(day, now);
        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var dayInput in structure.Days.Where(day => day.Id.HasValue))
        {
            var day = plan.GetDay(dayInput.Id!.Value);
            foreach (var exerciseInput in dayInput.Exercises.Where(item => item.Id.HasValue))
            {
                var exercise = day.GetExercise(exerciseInput.Id!.Value);
                if (!await ReorderSetsAsync(
                    exercise,
                    exerciseInput,
                    now,
                    cancellationToken))
                    return false;
            }
        }

        return true;
    }

    public void Reconcile(
        TrainingPlan plan,
        TrainingPlanStructureInput structure,
        DateTime now
    )
    {
        var desiredDayIds = structure.Days.Where(day => day.Id.HasValue)
            .Select(day => day.Id!.Value)
            .ToHashSet();

        foreach (var day in plan.Days.Where(day => !desiredDayIds.Contains(day.Id)).ToArray())
            plan.RemoveDay(day.Id, now);

        foreach (var dayInput in structure.Days.Where(day => day.Id.HasValue))
            ReconcileDay(plan, dayInput, now);

        foreach (var dayInput in structure.Days.Where(day => !day.Id.HasValue)
            .OrderBy(day => day.WeekNumber).ThenBy(day => day.DayOfWeek))
        {
            var day = plan.AddDay(
                dayInput.DayOfWeek,
                dayInput.WeekNumber,
                dayInput.Notes,
                now);
            AddExercises(day, dayInput.Exercises, now);
        }
    }

    private static void ReconcileDay(
        TrainingPlan plan,
        TrainingPlanStructureInput.TrainingDayInput input,
        DateTime now
    )
    {
        var day = plan.GetDay(input.Id!.Value);
        var desiredExerciseIds = input.Exercises.Where(item => item.Id.HasValue)
            .Select(item => item.Id!.Value)
            .ToHashSet();

        foreach (var item in day.Exercises.Where(item => !desiredExerciseIds.Contains(item.Id)).ToArray())
            day.RemoveExercise(item.Id, now);

        foreach (var exerciseInput in input.Exercises.Where(item => item.Id.HasValue))
            ReconcileExercise(day, exerciseInput, now);

        plan.UpdateDay(input.Id.Value, input.DayOfWeek, input.WeekNumber, input.Notes, now);
        AddExercises(day, input.Exercises.Where(item => !item.Id.HasValue), now);
    }

    private static void ReconcileExercise(
        TrainingPlanDay day,
        TrainingPlanStructureInput.DayExerciseInput input,
        DateTime now
    )
    {
        var exercise = day.GetExercise(input.Id!.Value);
        var desiredSetIds = input.Sets.Where(set => set.Id.HasValue)
            .Select(set => set.Id!.Value)
            .ToHashSet();

        foreach (var set in exercise.Sets.Where(set => !desiredSetIds.Contains(set.Id)).ToArray())
            exercise.RemoveSet(set.Id, now);

        foreach (var setInput in input.Sets.Where(set => set.Id.HasValue))
        {
            exercise.UpdateSet(
                setInput.Id!.Value,
                setInput.SetNumber,
                setInput.PlannedReps,
                setInput.PlannedWeightKg,
                setInput.RestSecondsMin,
                setInput.RestSecondsMax,
                now);
        }

        day.UpdateExercise(
            input.Id.Value,
            input.ExerciseId,
            input.OrderNumber,
            input.ExerciseGroupId,
            input.GroupPosition,
            input.Notes,
            now);
        AddSets(exercise, input.Sets.Where(set => !set.Id.HasValue), now);
    }

    private static void AddExercises(
        TrainingPlanDay day,
        IEnumerable<TrainingPlanStructureInput.DayExerciseInput> inputs,
        DateTime now
    )
    {
        foreach (var input in inputs
            .OrderBy(item => item.OrderNumber)
            .ThenBy(item => item.GroupPosition))
        {
            var exercise = day.AddExercise(
                input.ExerciseId,
                input.OrderNumber,
                input.ExerciseGroupId,
                input.GroupPosition,
                input.Notes,
                now);
            AddSets(exercise, input.Sets, now);
        }
    }

    private static void AddSets(
        TrainingPlanDayExercise exercise,
        IEnumerable<TrainingPlanStructureInput.ExerciseSetInput> inputs,
        DateTime now
    )
    {
        foreach (var input in inputs.OrderBy(set => set.SetNumber))
        {
            exercise.AddSet(
                input.SetNumber,
                input.PlannedReps,
                input.PlannedWeightKg,
                input.RestSecondsMin,
                input.RestSecondsMax,
                now);
        }
    }

    private static void RemoveOmittedNodes(
        TrainingPlan plan,
        TrainingPlanStructureInput structure,
        DateTime now
    )
    {
        var desiredDayIds = structure.Days.Where(day => day.Id.HasValue)
            .Select(day => day.Id!.Value)
            .ToHashSet();

        foreach (var day in plan.Days.Where(day => !desiredDayIds.Contains(day.Id)).ToArray())
            plan.RemoveDay(day.Id, now);

        foreach (var dayInput in structure.Days.Where(day => day.Id.HasValue))
        {
            var day = plan.GetDay(dayInput.Id!.Value);
            var desiredExerciseIds = dayInput.Exercises.Where(item => item.Id.HasValue)
                .Select(item => item.Id!.Value)
                .ToHashSet();

            foreach (var item in day.Exercises
                .Where(item => !desiredExerciseIds.Contains(item.Id)).ToArray())
            {
                day.RemoveExercise(item.Id, now);
            }

            foreach (var exerciseInput in dayInput.Exercises.Where(item => item.Id.HasValue))
            {
                var exercise = day.GetExercise(exerciseInput.Id!.Value);
                var desiredSetIds = exerciseInput.Sets.Where(set => set.Id.HasValue)
                    .Select(set => set.Id!.Value)
                    .ToHashSet();

                foreach (var set in exercise.Sets
                    .Where(set => !desiredSetIds.Contains(set.Id)).ToArray())
                {
                    exercise.RemoveSet(set.Id, now);
                }
            }
        }
    }

    private async Task<bool> ReorderDaysAsync(
        TrainingPlan plan,
        TrainingPlanStructureInput structure,
        DateTime now,
        CancellationToken cancellationToken = default
    )
    {
        var targets = structure.Days
            .Where(day => day.Id.HasValue)
            .ToDictionary(
                day => day.Id!.Value,
                day => (day.WeekNumber, day.DayOfWeek));

        var universe = Enumerable.Range(1, 52)
            .SelectMany(week => Enumerable.Range(0, 7)
                .Select(day => (WeekNumber: week, DayOfWeek: day)))
            .ToArray();

        while (plan.Days.Any(day =>
            targets.TryGetValue(day.Id, out var target) &&
            (day.WeekNumber, day.DayOfWeek) != target))
        {
            var occupied = plan.Days
                .Select(day => (day.WeekNumber, day.DayOfWeek))
                .ToHashSet();

            var free = universe.FirstOrDefault(slot => !occupied.Contains(slot));
            if (free == default)
                return false;

            var movable = plan.Days.First(day =>
                targets.TryGetValue(day.Id, out var target) &&
                (day.WeekNumber, day.DayOfWeek) != target);
            var desired = targets[movable.Id];
            var blocker = plan.Days.FirstOrDefault(day =>
                (day.WeekNumber, day.DayOfWeek) == desired);

            if (blocker is not null)
            {
                plan.UpdateDay(
                    blocker.Id,
                    free.DayOfWeek,
                    free.WeekNumber,
                    blocker.Notes,
                    now);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            plan.UpdateDay(
                movable.Id,
                desired.DayOfWeek,
                desired.WeekNumber,
                movable.Notes,
                now);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private async Task<bool> ReorderSetsAsync(
        TrainingPlanDayExercise exercise,
        TrainingPlanStructureInput.DayExerciseInput input,
        DateTime now,
        CancellationToken cancellationToken = default
    )
    {
        var targets = input.Sets
            .Where(set => set.Id.HasValue)
            .ToDictionary(set => set.Id!.Value, set => set.SetNumber);

        while (exercise.Sets.Any(set =>
            targets.TryGetValue(set.Id, out var target) &&
            set.SetNumber != target))
        {
            var occupied = exercise.Sets
                .Select(set => set.SetNumber)
                .ToHashSet();

            var free = Enumerable.Range(1, 15).FirstOrDefault(value => !occupied.Contains(value));
            if (free == 0)
                return false;

            var movable = exercise.Sets.First(set =>
                targets.TryGetValue(set.Id, out var target) &&
                set.SetNumber != target);

            var desired = targets[movable.Id];
            var blocker = exercise.Sets.FirstOrDefault(set => set.SetNumber == desired);

            if (blocker is not null)
            {
                exercise.UpdateSet(
                    blocker.Id,
                    free,
                    blocker.PlannedReps,
                    blocker.PlannedWeightKg,
                    blocker.RestSecondsMin,
                    blocker.RestSecondsMax,
                    now);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            exercise.UpdateSet(
                movable.Id,
                desired,
                movable.PlannedReps,
                movable.PlannedWeightKg,
                movable.RestSecondsMin,
                movable.RestSecondsMax,
                now);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private static void StageExerciseOrders(
        TrainingPlanDay day,
        DateTime now
    )
    {
        var usedOrders = day.Exercises.Select(item => item.OrderNumber).ToHashSet();
        var usedPositions = day.Exercises
            .Where(item => item.ExerciseGroupId.HasValue)
            .GroupBy(item => item.ExerciseGroupId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.GroupPosition!.Value).ToHashSet());
        var nextOrder = int.MaxValue;
        var nextPositions = new Dictionary<Guid, int>();

        foreach (var item in day.Exercises.OrderBy(item => item.Id))
        {
            while (usedOrders.Contains(nextOrder))
                nextOrder--;

            int? position = null;
            if (item.ExerciseGroupId.HasValue)
            {
                var groupId = item.ExerciseGroupId!.Value;
                var candidate = nextPositions.GetValueOrDefault(groupId, int.MaxValue);
                while (usedPositions[groupId].Contains(candidate))
                    candidate--;

                position = candidate;
                usedPositions[groupId].Add(candidate);
                nextPositions[groupId] = candidate - 1;
            }

            day.UpdateExercise(
                item.Id,
                item.ExerciseId,
                nextOrder,
                item.ExerciseGroupId,
                position,
                item.Notes,
                now);
            usedOrders.Add(nextOrder);
            nextOrder--;
        }
    }
}
