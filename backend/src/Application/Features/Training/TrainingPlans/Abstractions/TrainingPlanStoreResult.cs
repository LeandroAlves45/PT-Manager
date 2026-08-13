namespace Application.Features.Training.TrainingPlans.Abstractions;

/// <summary>Classifica resultados funcionais de escrita de planos de treino.</summary>
public sealed class TrainingPlanStoreResult
{
    public enum Status
    {
        Created,
        Updated,
        Replaced,
        Changed,
        AlreadyArchived,
        NotFound,
        Inactive,
        ClientNotFound,
        ExerciseReferenceNotFound,
        ExerciseReferenceInactive,
        StructureReferenceNotFound,
        StructureHasHistory,
        StructureReorderRequiresFreeSlot,
        ActivePlanConflict
    }

    public Status Kind { get; }
    public Guid? TrainingPlanId { get; }

    private TrainingPlanStoreResult(Status kind, Guid? trainingPlanId)
    {
        Kind = kind;
        TrainingPlanId = trainingPlanId;
    }

    public static TrainingPlanStoreResult ForCreated(Guid id) => WithId(Status.Created, id);
    public static TrainingPlanStoreResult ForUpdated(Guid id) => WithId(Status.Updated, id);
    public static TrainingPlanStoreResult ForReplaced(Guid id) => WithId(Status.Replaced, id);
    public static TrainingPlanStoreResult ForChanged() => new(Status.Changed, null);
    public static TrainingPlanStoreResult ForAlreadyArchived() => new(Status.AlreadyArchived, null);
    public static TrainingPlanStoreResult ForNotFound() => new(Status.NotFound, null);
    public static TrainingPlanStoreResult ForInactive() => new(Status.Inactive, null);
    public static TrainingPlanStoreResult ForClientNotFound() =>
        new(Status.ClientNotFound, null);
    public static TrainingPlanStoreResult ForExerciseReferenceNotFound() =>
        new(Status.ExerciseReferenceNotFound, null);
    public static TrainingPlanStoreResult ForExerciseReferenceInactive() =>
        new(Status.ExerciseReferenceInactive, null);
    public static TrainingPlanStoreResult ForStructureReferenceNotFound() =>
        new(Status.StructureReferenceNotFound, null);
    public static TrainingPlanStoreResult ForStructureHasHistory() =>
        new(Status.StructureHasHistory, null);
    public static TrainingPlanStoreResult ForStructureReorderRequiresFreeSlot() =>
        new(Status.StructureReorderRequiresFreeSlot, null);
    public static TrainingPlanStoreResult ForActivePlanConflict() =>
        new(Status.ActivePlanConflict, null);

    private static TrainingPlanStoreResult WithId(Status status, Guid id)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Training plan ID is required.", nameof(id));
        return new TrainingPlanStoreResult(status, id);
    }
}
