namespace Domain.Services;

/// <summary>
/// Série prescrita num dia do plano de treino, identificada pela prescrição e pelo número.
/// </summary>
public readonly record struct PlannedSetRef(Guid PrescriptionId, int SetNumber);

/// <summary>Dia do plano com as séries prescritas.</summary>
public sealed record PlannedTrainingDay(
    int WeekNumber,
    int DayOfWeek,
    IReadOnlyList<PlannedSetRef> Sets);

/// <summary>Série registada, já convertida para o dia local do personal trainer.</summary>
public readonly record struct PerformedSetRef(Guid PrescriptionId, int SetNumber, DateOnly LocalDate);

/// <summary>Resultado da adesão numa janela.</summary>
public sealed record TrainingAdherence(int PlannedSets, int LoggedSets, int? Percentage);

/// <summary>
/// Calcula a adesão ao treino = séries registadas ÷ séries planeadas.
/// Uma série só conta como registada se foi feita no dia em que o calendário cíclico a previa;
/// assim o numerador é sempre um subconjunto do denominador e o resultado nunca passa de 100 %.
/// </summary>
public static class TrainingAdherenceCalculator
{
    public static TrainingAdherence Calculate(
        DateOnly planStartDate,
        DateOnly? planEndDate,
        IReadOnlyList<PlannedTrainingDay> days,
        IEnumerable<PerformedSetRef> performed,
        DateOnly windowStart,
        DateOnly windowEnd)
    {
        ArgumentNullException.ThrowIfNull(days);
        ArgumentNullException.ThrowIfNull(performed);

        if (days.Count == 0 || windowEnd < windowStart)
            return new TrainingAdherence(0, 0, null);

        var cycleLength = days.Max(day => day.WeekNumber);
        var daysBySlot = days
            .GroupBy(day => new ScheduledSlot(day.WeekNumber, day.DayOfWeek))
            .ToDictionary(group => group.Key, group => group.SelectMany(day => day.Sets).ToList());

        // Conjunto (prescrição, série, data) previsto; HashSet elimina duplicados de registo.
        var planned = new HashSet<PerformedSetRef>();
        for (var date = windowStart; date <= windowEnd; date = date.AddDays(1))
        {
            var slot = TrainingPlanSchedule.Resolve(planStartDate, planEndDate, cycleLength, date);
            if (!slot.HasValue || !daysBySlot.TryGetValue(slot.Value, out var sets))
                continue;

            foreach (var set in sets)
                planned.Add(new PerformedSetRef(set.PrescriptionId, set.SetNumber, date));
        }

        if (planned.Count == 0)
            return new TrainingAdherence(0, 0, null);

        var logged = performed
            .Where(planned.Contains)
            .Distinct()
            .Count();

        var percentage = (int)Math.Round(
            logged * 100m / planned.Count,
            MidpointRounding.AwayFromZero);

        return new TrainingAdherence(planned.Count, logged, percentage);
    }
}
