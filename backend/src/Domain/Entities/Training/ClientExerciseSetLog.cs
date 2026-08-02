using Domain.Exceptions;
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
        DateTime now
    )
    {
        Validate(setNumber, weightKg, repsDone);

        Id = Guid.NewGuid();
        ClientId = clientId;
        TrainingPlanDayExerciseId = trainingPlanDayExerciseId;
        SetNumber = setNumber;
        WeightKg = weightKg;
        RepsDone = repsDone;
        Notes = NormalizeOptional(notes);
        LoggedAt = now;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Corrige um registo existente</summary>
    public void Correct(decimal weightKg, int repsDone, string? notes, DateTime now)
    {
        Validate(SetNumber, weightKg, repsDone);
        WeightKg = weightKg;
        RepsDone = repsDone;
        Notes = NormalizeOptional(notes);
        LoggedAt = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// Mêtodo privado para validação das cargas e repetições de uma série executada pelo cliente
    /// </summary>
    private static void Validate(int setNumber, decimal weightKg, int repsDone)
    {
        if (setNumber is < 1 or > 15 || weightKg < 0 || repsDone is < 0 or > 100)
            throw new DomainException("Set log values are outside their valid ranges.");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
