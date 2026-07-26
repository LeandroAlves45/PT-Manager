namespace Domain.Entities.Training;

/// <summary>
/// Registo real de uma série executada pelo cliente: carga e repetições efetivas,
/// com timestamp oficial.
/// </summary>
public class ClientExerciseSetLog
{
    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid TrainingPlanDayExerciseId { get; private set; }
    public int SetNumber { get; private set; }
    public decimal WeightKg { get; private set; }
    public int RepsDone { get; private set; }
    public string? Notes { get; private set; }
    /// <summary>
    /// Timestamp oficial do treino -> quando a série foi feita, não quando foi registada
    /// </summary>
    public DateTime LoggedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private ClientExerciseSetLog() { }

    /// <summary>
    /// Cria um registo de uma série executada pelo cliente
    /// </summary>
    public ClientExerciseSetLog(
        Guid clientId,
        Guid trainingPlanDayExerciseId,
        int setNumber,
        decimal weightKg,
        int repsDone,
        string? notes,
        DateTime loggedAt,
        DateTime now
    )
    {
        if (setNumber < 1 || setNumber > 15)
            throw new DomainException("Set number must be between 1 and 15.");
        ValidateWeightAndReps(weightKg, repsDone);

        Id = Guid.NewGuid();
        ClientId = clientId;
        TrainingPlanDayExerciseId = trainingPlanDayExerciseId;
        SetNumber = setNumber;
        WeightKg = weightKg;
        RepsDone = repsDone;
        Notes = notes;
        LoggedAt = loggedAt;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Corrige um registo existente</summary>
    public void Correct(decimal weightKg, int repsDone, string? notes, DateTime now)
    {
        ValidateWeightAndReps(weightKg, repsDone);

        WeightKg = weightKg;
        RepsDone = repsDone;
        Notes = notes;
        UpdatedAt = now;
    }

    /// <summary>
    /// Mêtodo privado para validação das cargas e repetições de uma série executada pelo cliente
    /// </summary>
    private void ValidateWeightAndReps(decimal weightKg, int repsDone)
    {
        if (weightKg < 0)
            throw new DomainException("Weight cannot be negative.");
        if (repsDone < 0 || repsDone > 100)
            throw new DomainException("Reps done must be between 0 and 100.");
    }
}
