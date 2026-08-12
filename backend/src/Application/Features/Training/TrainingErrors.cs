using Application.Errors;

namespace Application.Features.Training;

/// <summary>Disponibiliza erros estáveis dos casos de uso de treino.</summary>
public static class TrainingErrors
{
    public static readonly Error ExerciseNotFound = Error.Create(
        "exercise_not_found",
        ErrorCategory.NotFound,
        "The exercise was not found."
    );

    public static readonly Error GlobalExerciseReadOnly = Error.Create(
        "global_exercise_read_only",
        ErrorCategory.Forbidden,
        "Global exercises are read-only."
    );

    public static readonly Error TrainingPlanNotFound = Error.Create(
        "training_plan_not_found",
        ErrorCategory.NotFound,
        "The training plan was not found."
    );

    public static readonly Error ClientNotFound = Error.Create(
        "training_client_not_found",
        ErrorCategory.NotFound,
        "The client was not found."
    );

    public static readonly Error ExerciseReferenceNotFound = Error.Create(
        "training_exercise_reference_not_found",
        ErrorCategory.NotFound,
        "A referenced exercise was not found."
    );

    public static readonly Error ExerciseReferenceInactive = Error.Create(
        "training_exercise_reference_inactive",
        ErrorCategory.Conflict,
        "A new or changed exercise reference is inactive."
    );

    public static readonly Error StructureReferenceNotFound = Error.Create(
        "training_structure_reference_not_found",
        ErrorCategory.NotFound,
        "A referenced training structure node was not found."
    );

    public static readonly Error StructureHasHistory = Error.Create(
        "training_structure_has_history",
        ErrorCategory.Conflict,
        "Training history prevents the requested structural change."
    );

    public static readonly Error StructureReorderRequiresFreeSlot = Error.Create(
        "training_structure_reorder_requires_free_slot",
        ErrorCategory.Conflict,
        "The requested reorder requires one free position in the bounded structure."
    );

    public static readonly Error TrainingPlanInactive = Error.Create(
        "training_plan_inactive",
        ErrorCategory.Conflict,
        "The training plan is not active."
    );

    public static readonly Error SetNotFound = Error.Create(
        "training_set_not_found",
        ErrorCategory.NotFound,
        "The prescribed exercise set was not found."
    );

    public static readonly Error ExerciseSetLogNotFound = Error.Create(
        "exercise_set_log_not_found",
        ErrorCategory.NotFound,
        "The exercise set log was not found."
    );

    public static Error PerfomedAtInFuture() => Error.Validation([
        new ValidationError(
            "PerformedAt",
            "training_performed_at_in_future",
            "Perfomed at cannot be in the future."
        )
    ]);

    public static Error PerfomedAtOutsidePlan() => Error.Validation([
        new ValidationError(
            "PerformedAt",
            "training_performed_at_outside_plan",
            "Perfomed at must be inside the training plan date range."
        )
    ]);

    public static readonly Error ActiveTrainingPlanConflict = Error.Create(
        "active_training_plan_conflict",
        ErrorCategory.Conflict,
        "The client already has an active training plan."
    );
}
