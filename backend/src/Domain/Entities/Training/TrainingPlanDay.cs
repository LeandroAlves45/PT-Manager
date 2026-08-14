using Domain.Exceptions;
namespace Domain.Entities.Training;

/// <summary>
/// Dia de treino de um plano de treino. Dia da semana (ex: segunda...domingo)
/// </summary>
public sealed class TrainingPlanDay
{
    private readonly List<TrainingPlanDayExercise> _exercises = [];
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
    public IReadOnlyCollection<TrainingPlanDayExercise> Exercises => _exercises;

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
        if (trainingPlanId == Guid.Empty)
            throw new DomainException("Training plan ID is required.");
        Validate(dayOfWeek, weekNumber);

        Id = Guid.NewGuid();
        TrainingPlanId = trainingPlanId;
        DayOfWeek = dayOfWeek;
        WeekNumber = weekNumber;
        Notes = NormalizeOptional(notes);
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Atualiza a posição temporal e notas do dia.</summary>
    internal void Update(
        int dayOfWeek,
        int weekNumber,
        string? notes,
        DateTime now
    )
    {
        Validate(dayOfWeek, weekNumber);
        DayOfWeek = dayOfWeek;
        WeekNumber = weekNumber;
        Notes = NormalizeOptional(notes);
        UpdatedAt = now;
    }

    /// <summary>Adiciona um exercício prescrito ao dia.</summary>
    public TrainingPlanDayExercise AddExercise(
        Guid exerciseId,
        int orderNumber,
        Guid? exerciseGroupId,
        int? groupPosition,
        string? notes,
        DateTime now
    )
    {
        if (_exercises.Any(item => item.ExerciseGroupId is null &&
            exerciseGroupId is null && item.OrderNumber == orderNumber))
            throw new DomainException("An isolated exercise already uses this order number.");

        if (exerciseGroupId.HasValue && _exercises.Any(item =>
            item.ExerciseGroupId == exerciseGroupId && item.GroupPosition == groupPosition))
            throw new DomainException("An exercise group already uses this position.");

        var exercise = new TrainingPlanDayExercise(
            Id,
            exerciseId,
            orderNumber,
            exerciseGroupId,
            groupPosition,
            notes,
            now
        );
        _exercises.Add(exercise);
        UpdatedAt = now;
        return exercise;
    }

    /// <summary>Obtém um prescrição pertencente ao dia.</summary>
    public TrainingPlanDayExercise GetExercise(Guid dayexerciseId) =>
        _exercises.SingleOrDefault(item => item.Id == dayexerciseId)
        ?? throw new DomainException("Exercise prescription does not belong to this day.");

    /// <summary>Atualiza a prescrição depois das verificações de histórico.</summary>
    public void UpdateExercise(
        Guid dayexerciseId,
        Guid exerciseId,
        int orderNumber,
        Guid? exerciseGroupId,
        int? groupPosition,
        string? notes,
        DateTime now
    )
    {
        var exercise = GetExercise(dayexerciseId);
        if (_exercises.Any(item => item.Id != dayexerciseId &&
            item.ExerciseGroupId is null && exerciseGroupId is null &&
            item.OrderNumber == orderNumber))
            throw new DomainException("An isolated exercise already uses this order number.");

        if (exerciseGroupId.HasValue && _exercises.Any(item =>
            item.Id != dayexerciseId && item.ExerciseGroupId == exerciseGroupId &&
            item.GroupPosition == groupPosition))
            throw new DomainException("An exercise group already uses this position.");

        exercise.Update(
            exerciseId,
            orderNumber,
            exerciseGroupId,
            groupPosition,
            notes,
            now
        );
        UpdatedAt = now;
    }

    /// <summary>Remove uma prescrição autorizada como não histórica.</summary>
    public void RemoveExercise(Guid dayexerciseId, DateTime now)
    {
        var exercise = GetExercise(dayexerciseId);
        _exercises.Remove(exercise);
        UpdatedAt = now;
    }

    private static void Validate(int dayOfWeek, int weekNumber)
    {
        if (dayOfWeek is < 0 or > 6)
            throw new DomainException("Day of week must be between 0 and 6.");
        if (weekNumber is < 1 or > 52)
            throw new DomainException("Week number must be between 1 and 52.");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
