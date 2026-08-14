using Domain.Exceptions;
namespace Domain.Entities.Training;

/// <summary>
/// Registo real de uma série executada pelo cliente: carga e repetições efetivas,
/// com timestamp oficial.
/// </summary>
public sealed class ClientExerciseSetLog
{
    public Guid Id { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid TrainingPlanDayExerciseId { get; private set; }
    public int SetNumber { get; private set; }
    public decimal WeightKg { get; private set; }
    public int RepsDone { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset PerformedAt { get; private set; }
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
        DateTimeOffset performedAt,
        DateTime now
    )
    {
        ValidateIdentifiers(clientId, trainingPlanDayExerciseId);
        ValidateValues(setNumber, weightKg, repsDone);

        Id = Guid.NewGuid();
        ClientId = clientId;
        TrainingPlanDayExerciseId = trainingPlanDayExerciseId;
        SetNumber = setNumber;
        ApplyValues(weightKg, repsDone, notes, performedAt);
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Corrige valores executados e instante, preservando a identidade.</summary>
    public void Correct(
        decimal weightKg,
        int repsDone,
        string? notes,
        DateTimeOffset performedAt,
        DateTime now)
    {
        ValidateValues(SetNumber, weightKg, repsDone);
        ApplyValues(weightKg, repsDone, notes, performedAt);
        UpdatedAt = now;
    }

    private void ApplyValues(
        decimal weightKg,
        int repsDone,
        string? notes,
        DateTimeOffset performedAt
    )
    {
        WeightKg = weightKg;
        RepsDone = repsDone;
        Notes = NormalizeOptional(notes);
        PerformedAt = performedAt.ToUniversalTime();
    }

    private static void ValidateIdentifiers(Guid clientId, Guid trainingPlanDayExerciseId)
    {
        if (clientId == Guid.Empty)
            throw new DomainException("Client ID is required.");
        if (trainingPlanDayExerciseId == Guid.Empty)
            throw new DomainException("Training plan day exercise ID is required.");
    }

    /// <summary>
    /// Mêtodo privado para validação das cargas e repetições de uma série executada pelo cliente
    /// </summary>
    private static void ValidateValues(int setNumber, decimal weightKg, int repsDone)
    {
        if (setNumber is < 1 or > 15)
            throw new DomainException("Set number must be between 1 and 15.");
        if (weightKg < 0)
            throw new DomainException("Weight cannot be negative.");
        if (repsDone is < 0 or > 100)
            throw new DomainException("Reps done must be between 0 and 100.");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
