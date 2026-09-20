namespace Domain.Services;

/// <summary>
/// Posição de uma data no calendário de um plano de treino: semana do plano (1..N) e dia da
/// semana no formato do schema (0 =  segunda ... 6 = domingo).
/// </summary>
public readonly record struct ScheduledSlot(int WeekNumber, int DayOfWeek);

/// <summary>
/// Converte datas locais em posições cíclicas de um plano de treino. As semanas contam-se
/// alinhadas à segunda-feira a partir da semana de <c>StartDate</c>; depois da última semana
/// definida o plano recomeça na semana 1. Função pura, sem relógio
/// nem fuso: o chamador fornece a data já convertida para o dia local do trainer.
/// </summary>
public static class TrainingPlanSchedule
{
    public const int MaximumCycleLengthWeeks = 52;

    /// <summary>Converte um dia da semana (domingo = 0) no dia do schema (segunda = 0)</summary>
    public static int ToPlanDayOfWeek(DateOnly date) => ((int)date.DayOfWeek + 6) % 7;

    public static DateOnly WeekStart(DateOnly date) => date.AddDays(-ToPlanDayOfWeek(date));

    public static ScheduledSlot? Resolve(
        DateOnly startDate,
        DateOnly? endDate,
        int cycleLengthWeeks,
        DateOnly date)
    {
        EnsureCycleLength(cycleLengthWeeks);

        if (date < startDate || endDate.HasValue && date > endDate.Value)
            return null;

        // Semanas alinhadas à segunda: um plano que começa a uma quarta tem a semana 1 a
        // terminar no domingo seguinte, como no calendário que o personal trainer vê.
        var weeksSinceStart = (WeekStart(date).DayNumber - WeekStart(startDate).DayNumber) / 7;

        return new ScheduledSlot(
            weeksSinceStart % cycleLengthWeeks + 1,
            ToPlanDayOfWeek(date));
    }

    /// <summary>
    /// Procura a primeira data depois de <paramref name="afterDate"/> cuja posição cíclica tem
    /// treino. Percorre no máximo um ciclo completo (<c>7 × cycleLengthWeeks</c> dias), o que
    /// basta para encontrar qualquer dia definido; devolve null quando o plano acaba antes ou
    /// quando nenhum dia tem treino.
    /// </summary>
    public static DateOnly? FindNextScheduledDate(
        DateOnly startDate,
        DateOnly? endDate,
        int cycleLengthWeeks,
        DateOnly afterDate,
        IReadOnlySet<ScheduledSlot> scheduledSlots)
    {
        EnsureCycleLength(cycleLengthWeeks);
        ArgumentNullException.ThrowIfNull(scheduledSlots);

        if (scheduledSlots.Count == 0)
            return null;

        var candidate = afterDate < startDate ? startDate : afterDate.AddDays(1);
        var searchEnd = candidate.AddDays(cycleLengthWeeks * 7);

        for (; candidate < searchEnd; candidate = candidate.AddDays(1))
        {
            if (endDate.HasValue && candidate > endDate.Value)
                return null;

            var slot = Resolve(startDate, endDate, cycleLengthWeeks, candidate);
            if (slot.HasValue && scheduledSlots.Contains(slot.Value))
                return candidate;
        }

        return null;
    }

    private static void EnsureCycleLength(int cycleLengthWeeks)
    {
        if (cycleLengthWeeks is < 1 or > MaximumCycleLengthWeeks)
            throw new ArgumentOutOfRangeException(
                nameof(cycleLengthWeeks),
                cycleLengthWeeks,
                "The training plan cycle must have between 1 and 52 weeks.");
    }
}
