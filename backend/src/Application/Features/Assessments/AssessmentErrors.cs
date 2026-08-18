using Application.Errors;

namespace Application.Features.Assessments;

/// <summary>Erros funcionais estáveis de avaliações.</summary>
public static class AssessmentErrors
{
    public static readonly Error TrainerOnly = Error.Create(
        "assessment_trainer_only",
        ErrorCategory.Forbidden,
        "Only a personal trainer can manage assessments."
    );

    public static readonly Error ClientOnly = Error.Create(
        "check_in_client_only",
        ErrorCategory.Forbidden,
        "Only the associated client can answer a check-in."
    );

    public static readonly Error InitialAssessmentNotFound = Error.Create(
        "initial_assessment_not_found",
        ErrorCategory.NotFound,
        "Initial assessment was not found."
    );

    public static readonly Error InitialAssessmentAlreadyExists = Error.Create(
        "initial_assessment_already_exists",
        ErrorCategory.Conflict,
        "The client already has an initial assessment."
    );

    public static readonly Error ClientInactive = Error.Create(
        "assessment_client_inactive",
        ErrorCategory.Conflict,
        "An archived client cannot receive or answer a new assessment."
    );

    public static readonly Error CheckInNotFound = Error.Create(
        "check_in_not_found",
        ErrorCategory.NotFound,
        "Check-in was not found."
    );

    public static readonly Error CheckInDateConflict = Error.Create(
        "check_in_date_conflict",
        ErrorCategory.Conflict,
        "The client already has a check-in on that date."
    );

    public static readonly Error CheckInDateNotAllowed = Error.Validation(
    [
        new ValidationError(
            "CheckInDate",
            "check_in_date_not_allowed",
            "The check-in date is not allowed."
        )
    ]);

    public static readonly Error CheckInAlreadyAnswered = Error.Create(
        "check_in_already_answered",
        ErrorCategory.Conflict,
        "The check-in has already been answered."
    );

    public static readonly Error CheckInCancelled = Error.Create(
        "check_in_cancelled",
        ErrorCategory.Conflict,
        "A cancelled check-in cannot be answered or changed."
    );

    public static readonly Error CheckInWrongDay = Error.Create(
        "check_in_wrong_day",
        ErrorCategory.Conflict,
        "The check-in can only be answered on its scheduled local day."
    );

    public static readonly Error CheckInCannotBeRescheduled = Error.Create(
        "check_in_cannot_be_rescheduled",
        ErrorCategory.Conflict,
        "The check-in can no longer be rescheduled."
    );

    public static readonly Error CheckInCannotBeCancelled = Error.Create(
        "check_in_cannot_be_cancelled",
        ErrorCategory.Conflict,
        "The check-in can no longer be cancelled."
    );

    public static readonly Error CheckInNotAnswered = Error.Create(
        "check_in_not_answered",
        ErrorCategory.Conflict,
        "Only an answered check-in can be corrected."
    );
}
