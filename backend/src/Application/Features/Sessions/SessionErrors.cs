using Application.Errors;

namespace Application.Features.Sessions;

/// <summary>Erros funcionais estáveis da gestão de sessões.</summary>
public static class SessionErrors
{
    public static readonly Error SessionNotFound = Error.Create(
        "session_not_found",
        ErrorCategory.NotFound,
        "Session was not found."
    );

    public static readonly Error TrainerOnly = Error.Create(
        "session_trainer_only",
        ErrorCategory.Forbidden,
        "Only a personal trainer can modify sessions."
    );

    public static readonly Error ClientInactive = Error.Create(
        "session_client_inactive",
        ErrorCategory.Conflict,
        "An inactive client cannot receive new sessions."
    );

    public static readonly Error PackNotAvailable = Error.Create(
        "session_pack_not_available",
        ErrorCategory.NotFound,
        "The selected client session pack was not found or is not available."
    );

    public static readonly Error ClientDayConflict = Error.Create(
        "session_client_day_conflict",
        ErrorCategory.Conflict,
        "The client already has a scheduled session on that local day.");

    public static readonly Error TrainerScheduleConflict = Error.Create(
        "session_schedule_conflict",
        ErrorCategory.Conflict,
        "The personal trainer already has an overlapping scheduled session."
    );

    public static readonly Error InvalidState = Error.Create(
        "session_invalid_state",
        ErrorCategory.Conflict,
        "The session state is incompatible with this operation."
    );

    public static readonly Error PackBalanceUnavailable = Error.Create(
        "session_pack_balance_unavailable",
        ErrorCategory.Conflict,
        "The selected pack has no remaining session balance."
    );

    public static readonly Error TransitionTooEarly = Error.Create(
        "session_transition_too_early",
        ErrorCategory.Conflict,
        "The session cannot be completed or marked as no-show before its starts."
    );

    public static readonly Error StartsAtNotFuture = Error.Validation(
    [
        new ValidationError(
            "StartsAt",
            "session_starts_at_not_future",
            "Session start must be in the future."
        )
    ]);
}
