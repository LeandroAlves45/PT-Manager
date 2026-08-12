using Domain.Exceptions;
namespace Domain.Entities.Training;

/// <summary>
/// Exercício prescrito num dia de treino, com posição e notas do Personal Trainer.
/// </summary>
public class TrainingPlanDayExercise
{
    private readonly List<ExerciseSet> _sets = [];
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
    public IReadOnlyCollection<ExerciseSet> Sets => _sets;

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
        Validate(trainingPlanDayId, exerciseId, orderNumber, exerciseGroupId, groupPosition);

        Id = Guid.NewGuid();
        TrainingPlanDayId = trainingPlanDayId;
        Apply(exerciseId, orderNumber, exerciseGroupId, groupPosition, notes);
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Atualiza a prescrição depois de a camada persistente autorizar.</summary>
    internal void Update(
        Guid exerciseId,
        int orderNumber,
        Guid? exerciseGroupId,
        int? groupPosition,
        string? notes,
        DateTime now
    )
    {
        Validate(TrainingPlanDayId, exerciseId, orderNumber, exerciseGroupId, groupPosition);
        Apply(exerciseId, orderNumber, exerciseGroupId, groupPosition, notes);
        UpdatedAt = now;
    }

    /// <summary>Adiciona uma série com SetNumber único na prescrição.</summary>
    public ExerciseSet AddSet(
        int setNumber,
        int? plannedReps,
        decimal? plannedWeightKg,
        int? restSecondsMin,
        int? restSecondsMax,
        DateTime now
    )
    {
        if (_sets.Any(set => set.SetNumber == setNumber))
            throw new DomainException("Set number already exists for this exercise.");

        var set = new ExerciseSet(
            Id,
            setNumber,
            plannedReps,
            plannedWeightKg,
            restSecondsMin,
            restSecondsMax,
            now
        );
        _sets.Add(set);
        UpdatedAt = now;
        return set;
    }

    /// <summary>Obtém uma série pertencente à prescrição.</summary>
    public ExerciseSet GetSet(Guid setId) =>
        _sets.SingleOrDefault(set => set.Id == setId)
        ?? throw new DomainException("Exercise set does not belong to this prescription.");

    /// <summary>Atualiza uma série mantendo a respetiva identidade.</summary>
    public void UpdateSet(
        Guid setId,
        int setNumber,
        int? plannedReps,
        decimal? plannedWeightKg,
        int? restSecondsMin,
        int? restSecondsMax,
        DateTime now
    )
    {
        if (_sets.Any(set => set.Id != setId && set.SetNumber == setNumber))
            throw new DomainException("Set number already exists for this exercise.");

        GetSet(setId).Update(
            setNumber,
            plannedReps,
            plannedWeightKg,
            restSecondsMin,
            restSecondsMax,
            now
        );
        UpdatedAt = now;
    }

    /// <summary>Remove uma série autorizada como não histórica.</summary>
    public void RemoveSet(Guid setId, DateTime now)
    {
        var set = GetSet(setId);
        _sets.Remove(set);
        UpdatedAt = now;
    }

    private void Apply(
        Guid exerciseId,
        int orderNumber,
        Guid? exerciseGroupId,
        int? groupPosition,
        string? notes
    )
    {
        ExerciseId = exerciseId;
        OrderNumber = orderNumber;
        ExerciseGroupId = exerciseGroupId;
        GroupPosition = groupPosition;
        Notes = NormalizeOptional(notes);
    }

    private static void Validate(
        Guid trainingPlanDayId,
        Guid exerciseId,
        int orderNumber,
        Guid? exerciseGroupId,
        int? groupPosition
    )
    {
        if (trainingPlanDayId == Guid.Empty)
            throw new DomainException("Training plan day ID is required.");
        if (exerciseId == Guid.Empty)
            throw new DomainException("Exercise ID is required.");
        if (orderNumber < 1)
            throw new DomainException("Order number must be greater than zero.");
        if (!exerciseGroupId.HasValue && groupPosition.HasValue)
            throw new DomainException("An isolated exercise cannot have a group position.");
        if (exerciseGroupId == Guid.Empty)
            throw new DomainException("Exercise group ID cannot be empty.");
        if (exerciseGroupId.HasValue && (!groupPosition.HasValue || groupPosition <= 0))
            throw new DomainException("A grouped exercise requires a positive group position.");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
