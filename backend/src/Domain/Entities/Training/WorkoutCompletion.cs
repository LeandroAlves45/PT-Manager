using Domain.Exceptions;

namespace Domain.Entities.Training;

/// <summary>
/// Marca o treino como concluído pelo cliente para um dia do plano numa data local do personal
/// trainer. Aceita treinos parciais e não afeta sessões nem packs.
/// </summary>
public sealed class WorkoutCompletion
{
    public const int NotesMaxLength = 500;

    public Guid Id { get; private set; }
    public Guid OwnerTrainerId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid TrainingPlanId { get; private set; }
    public Guid TrainingPlanDayId { get; private set; }
    public DateOnly LocalDate { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CompletedAt { get; private set; }

    private WorkoutCompletion() { }

    /// <summary>Regista a conclusão de um dia de treino.</summary>
    public WorkoutCompletion(
        Guid ownerTrainerId,
        Guid clientId,
        Guid trainingPlanId,
        Guid trainingPlanDayId,
        DateOnly localDate,
        string? notes,
        DateTime now)
    {
        if (ownerTrainerId == Guid.Empty || clientId == Guid.Empty)
            throw new DomainException("Owner trainer ID and client ID are required.");
        if (trainingPlanId == Guid.Empty || trainingPlanDayId == Guid.Empty)
            throw new DomainException("Training plan ID and training plan day ID are required.");

        var normalizedNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        if (normalizedNotes is { Length: > NotesMaxLength })
            throw new DomainException("Workout completion notes cannot exceed 500 characters.");

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ClientId = clientId;
        TrainingPlanId = trainingPlanId;
        TrainingPlanDayId = trainingPlanDayId;
        LocalDate = localDate;
        Notes = normalizedNotes;
        CompletedAt = now;
    }
}
