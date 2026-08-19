using Application.Errors;

namespace Application.Features.Supplements;

/// <summary>Erros funcionais estáveis de Supplements.</summary>
public static class SupplementErrors
{
    public static readonly Error SupplementIdRequired = Error.Validation([
        new ValidationError("SupplementId", "supplement_id_required", "Supplement ID is required.")
    ]);

    public static readonly Error AssignmentIdRequired = Error.Validation([
        new ValidationError(
            "AssignmentId",
            "supplement_assignment_id_required",
            "Assignment ID is required.")
    ]);

    public static readonly Error TrainerOnly = Error.Create(
        "supplement_trainer_only",
        ErrorCategory.Forbidden,
        "Only a personal trainer can manage private supplements and assignments.");

    public static readonly Error ClientOnly = Error.Create(
        "supplement_client_only",
        ErrorCategory.Forbidden,
        "Only the associated client can read these supplement assignments.");

    public static readonly Error AdministratorOnly = Error.Create(
        "supplement_administrator_only",
        ErrorCategory.Forbidden,
        "Only an authorized superuser can manage global supplements.");

    public static readonly Error SupplementNotFound = Error.Create(
        "supplement_not_found",
        ErrorCategory.NotFound,
        "Supplement was not found.");

    public static readonly Error GlobalSupplementReadOnly = Error.Create(
        "global_supplement_read_only",
        ErrorCategory.Forbidden,
        "Global supplements are read-only for personal trainers.");

    public static readonly Error SupplementInactive = Error.Create(
        "supplement_inactive",
        ErrorCategory.Conflict,
        "An archived supplement cannot receive a new reference or be modified. Reactivate it first.");

    public static readonly Error AssignmentNotFound = Error.Create(
        "supplement_assignment_not_found",
        ErrorCategory.NotFound,
        "Supplement assignment was not found.");

    public static readonly Error AssignmentAlreadyExists = Error.Create(
        "supplement_assignment_already_exists",
        ErrorCategory.Conflict,
        "The client already has an assignment for this supplement. Use reactivate when it is inactive.");

    public static readonly Error ClientInactive = Error.Create(
        "supplement_client_inactive",
        ErrorCategory.Conflict,
        "An archived client cannot receive or change a supplement assignment.");

    public static readonly Error GlobalSupplementHasReferences = Error.Create(
        "global_supplement_has_references",
        ErrorCategory.Conflict,
        "A referenced global supplement cannot be deleted. Archive it instead.");
}
