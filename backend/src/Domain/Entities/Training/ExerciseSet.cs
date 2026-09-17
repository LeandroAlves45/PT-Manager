using Domain.Exceptions;
using Domain.ValueObjects;

namespace Domain.Entities.Training;

/// <summary>
/// Série planeada de um exercício prescrito: número da série (1-15),
/// repetições e carga alvo (opcional) e descanso e RPE planeado (opcional).
/// </summary>
public sealed class ExerciseSet
{
    public Guid Id { get; private set; }
    public Guid TrainingPlanDayExerciseId { get; private set; }
    public int SetNumber { get; private set; }
    public int? PlannedReps { get; private set; }
    public decimal? PlannedWeightKg { get; private set; }
    public int? RestSecondsMin { get; private set; }
    public int? RestSecondsMax { get; private set; }
    public decimal? PlannedRpe { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private ExerciseSet() { }

    /// <summary>
    /// Cria uma série planeada de um exercício prescrito
    /// </summary>
    public ExerciseSet(
        Guid trainingPlanDayExerciseId,
        int setNumber,
        int? plannedReps,
        decimal? plannedWeightKg,
        int? restSecondsMin,
        int? restSecondsMax,
        DateTime now,
        decimal? plannedRpe = null
    )
    {
        if (trainingPlanDayExerciseId == Guid.Empty)
            throw new DomainException("Training plan day exercise ID is required.");
        Validate(
            setNumber, plannedReps, plannedWeightKg, restSecondsMin, restSecondsMax, plannedRpe);

        Id = Guid.NewGuid();
        TrainingPlanDayExerciseId = trainingPlanDayExerciseId;
        Apply(setNumber, plannedReps, plannedWeightKg, restSecondsMin, restSecondsMax, plannedRpe);
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Substitui todos os valores planeados da série.</summary>
    internal void Update(
        int setNumber,
        int? plannedReps,
        decimal? plannedWeightKg,
        int? restSecondsMin,
        int? restSecondsMax,
        decimal? plannedRpe,
        DateTime now
    )
    {
        Validate(setNumber, plannedReps, plannedWeightKg, restSecondsMin, restSecondsMax, plannedRpe);
        Apply(setNumber, plannedReps, plannedWeightKg, restSecondsMin, restSecondsMax, plannedRpe);
        UpdatedAt = now;
    }

    private void Apply(
        int setNumber,
        int? plannedReps,
        decimal? plannedWeightKg,
        int? restSecondsMin,
        int? restSecondsMax,
        decimal? plannedRpe
    )
    {
        SetNumber = setNumber;
        PlannedReps = plannedReps;
        PlannedWeightKg = plannedWeightKg;
        RestSecondsMin = restSecondsMin;
        RestSecondsMax = restSecondsMax;
        PlannedRpe = plannedRpe;
    }

    private static void Validate(
        int setNumber,
        int? plannedReps,
        decimal? plannedWeightKg,
        int? restSecondsMin,
        int? restSecondsMax,
        decimal? plannedRpe
    )
    {
        if (setNumber is < 1 or > 15)
            throw new DomainException("Set number must be between 1 and 15.");
        if (plannedReps is <= 0)
            throw new DomainException("Planned reps must be greater than zero.");
        if (plannedWeightKg is < 0 || restSecondsMin is < 0 || restSecondsMax is < 0)
            throw new DomainException("Load and rest values cannot be negative.");
        if (restSecondsMin.HasValue && restSecondsMax.HasValue && restSecondsMin.Value > restSecondsMax.Value)
            throw new DomainException("Minimum rest cannot exceed maximum rest.");
        RpeScale.EnsureValid(plannedRpe, "Planned RPE");
    }
}
