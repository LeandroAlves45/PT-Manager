using Domain.Exceptions;
namespace Domain.Entities.Training;

/// <summary>
/// Exercício prescrito num dia de treino, com posição e notas do Personal Trainer.
/// </summary>
public class TrainingPlanDayExercise
{
    public Guid Id { get; private set; }
    public Guid TrainingPlanDayId { get; private set; }
    public Guid ExerciseId { get; private set; }
    public int OrderNumber { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private TrainingPlanDayExercise() { }

    /// <summary>
    /// Cria a prescrição de um exercício para um dia de treino
    /// </summary>
    public TrainingPlanDayExercise(
        Guid trainingPlanDayId,
        Guid exerciseId,
        int orderNumber,
        string? notes,
        DateTime now
    )
    {
        if (orderNumber < 1)
            throw new DomainException("Order number must be greater than 0.");

        Id = Guid.NewGuid();
        TrainingPlanDayId = trainingPlanDayId;
        ExerciseId = exerciseId;
        OrderNumber = orderNumber;
        Notes = notes;
        CreatedAt = now;
        UpdatedAt = now;
    }
}
