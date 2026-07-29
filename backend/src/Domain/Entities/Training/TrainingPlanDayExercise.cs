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
    /// <summary>Identificador opaco de uma supersérie, tri-set ou circuito.</summary>
    public Guid? ExerciseGroupId { get; private set; }
    /// <summary>Posição interna no grupo; null num exercício isolado.</summary>
    public int? GroupPosition { get; private set; }
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
        Guid? exerciseGroupId,
        int? groupPosition,
        string? notes,
        DateTime now
    )
    {
        if (orderNumber < 1)
            throw new DomainException("Order number must be greater than 0.");
        if (!exerciseGroupId.HasValue && groupPosition.HasValue)
            throw new DomainException("An isolated exercise cannot have a group position.");
        if (exerciseGroupId.HasValue && exerciseGroupId.Value == Guid.Empty)
            throw new DomainException("Exercise group ID cannot be empty.");
        if (exerciseGroupId.HasValue && (!groupPosition.HasValue || groupPosition <= 0))
            throw new DomainException("A grouped exercise requires a positive group position.");

        Id = Guid.NewGuid();
        TrainingPlanDayId = trainingPlanDayId;
        ExerciseId = exerciseId;
        OrderNumber = orderNumber;
        ExerciseGroupId = exerciseGroupId;
        GroupPosition = groupPosition;
        Notes = notes;
        CreatedAt = now;
        UpdatedAt = now;
    }
}
