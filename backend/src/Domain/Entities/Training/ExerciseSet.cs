using Domain.Exceptions;
namespace Domain.Entities.Training;

/// <summary>
/// Série planeada de um exercício prescrito: número da série (1-15),
/// repetições e carga alvo (opcional) e descanso
/// </summary>
public class ExerciseSet
{
    public Guid Id { get; private set; }
    public Guid TrainingPlanDayExerciseId { get; private set; }
    public int SetNumber { get; private set; }
    public int? PlannedReps { get; private set; }
    public decimal? PlannedWeightKg { get; private set; }
    public int? RestSecondsMin { get; private set; }
    public int? RestSecondsMax { get; private set; }
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
        DateTime now
    )
    {
        Validate(setNumber, plannedReps, plannedWeightKg, restSecondsMin, restSecondsMax);

        Id = Guid.NewGuid();
        TrainingPlanDayExerciseId = trainingPlanDayExerciseId;
        SetNumber = setNumber;
        PlannedReps = plannedReps;
        PlannedWeightKg = plannedWeightKg;
        RestSecondsMin = restSecondsMin;
        RestSecondsMax = restSecondsMax;
        CreatedAt = now;
        UpdatedAt = now;
    }

    private static void Validate(
        int setNumber,
        int? plannedReps,
        decimal? plannedWeightKg,
        int? restSecondsMin,
        int? restSecondsMax
    )
    {
        if (setNumber is < 1 || setNumber > 15)
            throw new DomainException("Set number must be between 1 and 15.");
        if (plannedReps <= 0 || plannedWeightKg < 0 || restSecondsMin < 0 || restSecondsMax < 0)
            throw new DomainException("Set values cannot violate their non-negative ranges.");
        if (restSecondsMin.HasValue && restSecondsMax.HasValue && restSecondsMin.Value > restSecondsMax.Value)
            throw new DomainException("Minimum rest cannot exceed maximum rest.");
    }
}
