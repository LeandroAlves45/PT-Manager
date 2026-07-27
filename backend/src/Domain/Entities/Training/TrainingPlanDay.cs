using Domain.Exceptions;
namespace Domain.Entities.Training;

/// <summary>
/// Dia de treino de um plano de treino. Dia da semana (ex: segunda...domingo)
/// </summary>
public class TrainingPlanDay
{
    public Guid Id { get; private set; }
    public Guid TrainingPlanId { get; private set; }
    public int DayOfWeek { get; private set; }
    /// <summary>
    /// Número da semana dentro do plano (1-52)
    /// </summary>
    public int WeekNumber { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private TrainingPlanDay() { }

    /// <summary>
    /// Cria um dia de treino para um plano de treino
    /// </summary>
    public TrainingPlanDay(
        Guid trainingPlanId,
        int dayOfWeek,
        int weekNumber,
        string? notes,
        DateTime now
    )
    {
        if (dayOfWeek < 0 || dayOfWeek > 6)
            throw new DomainException("Day of week must be between 0 (Monday) and 6 (Sunday).");
        if (weekNumber < 1 || weekNumber > 52)
            throw new DomainException("Week number must be between 1 and 52.");

        Id = Guid.NewGuid();
        TrainingPlanId = trainingPlanId;
        DayOfWeek = dayOfWeek;
        WeekNumber = weekNumber;
        Notes = notes;
        CreatedAt = now;
        UpdatedAt = now;
    }
}
