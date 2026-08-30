using Application.Errors;

namespace Application.Features.Authentication;

/// <summary>Erros estáveis dos casos de uso de autenticação local.</summary>
public static class AuthenticationErrors
{
    public static readonly Error TrainerOnly = Error.Create(
        "authentication_trainer_only",
        ErrorCategory.Forbidden,
        "Only a personal trainer can perform this operation."
    );

    public static readonly Error ClientOnly = Error.Create(
        "authentication_client_only",
        ErrorCategory.Forbidden,
        "Only a client account can perform this operation."
    );

    public static readonly Error DuplicateEmail = Error.Create(
        "authentication_email_already_exists",
        ErrorCategory.Conflict,
        "An account with this email already exists."
    );

    /// <summary>Converte detalhes internos do Identity numa falha de validação segura.</summary>
    public static Error RegistrationRejected() => Error.Validation([
        new ValidationError(
            "Password",
            "authentication_registration_rejected",
            "The account could not be created with the supplied credentials."
        )
    ]);

    public static readonly Error ConcurrencyConflict = Error.Create(
        "authentication_concurrency_conflict",
        ErrorCategory.Conflict,
        "The authentication state changed concurrently. Try again."
    );

    public static readonly Error EmailDeliveryUnavailable = Error.Create(
        "authentication_email_delivery_unavailable",
        ErrorCategory.ExternalDependency,
        "The authentication email could not be delivered. Try again."
    );

    public static readonly Error AccountInactive = Error.Create(
        "authentication_account_inactive",
        ErrorCategory.Forbidden,
        "The account is not active."
    );

    public static readonly Error EmailAlreadyConfirmed = Error.Create(
        "authentication_email_already_confirmed",
        ErrorCategory.Conflict,
        "The email is already confirmed."
    );

    public static readonly Error ConfirmationTokenInvalid = Error.Create(
        "authentication_confirmation_token_invalid",
        ErrorCategory.Unauthorized,
        "The email confirmation token is invalid."
    );

    public static readonly Error ConfirmationTokenExpired = Error.Create(
        "authentication_confirmation_token_expired",
        ErrorCategory.Unauthorized,
        "The email confirmation token has expired."
    );

    public static readonly Error ConfirmationTokenConsumed = Error.Create(
        "authentication_confirmation_token_consumed",
        ErrorCategory.Conflict,
        "The email confirmation token was already used."
    );

    public static readonly Error ClientNotFound = Error.Create(
        "authentication_client_not_found",
        ErrorCategory.NotFound,
        "The client was not found."
    );

    public static readonly Error ClientInactive = Error.Create(
        "authentication_client_inactive",
        ErrorCategory.Conflict,
        "An archived client cannot receive an invitation."
    );

    public static readonly Error InvitationEmailMismatch = Error.Create(
        "authentication_invitation_email_mismatch",
        ErrorCategory.Conflict,
        "The invitation email does not match the account."
    );

    public static readonly Error InvitationInvalid = Error.Create(
        "authentication_invitation_invalid",
        ErrorCategory.Unauthorized,
        "The client invitation is invalid."
    );

    public static readonly Error InvitationExpired = Error.Create(
        "authentication_invitation_expired",
        ErrorCategory.Unauthorized,
        "The client invitation has expired."
    );

    public static readonly Error InvitationConsumed = Error.Create(
        "authentication_invitation_consumed",
        ErrorCategory.Conflict,
        "The client invitation was already used."
    );

    public static readonly Error TransferApprovalRequired = Error.Create(
        "authentication_transfer_approval_required",
        ErrorCategory.Conflict,
        "Explicit approval is required to transfer the active client relationship."
    );

    public static readonly Error RelationshipConflict = Error.Create(
        "authentication_relationship_conflict",
        ErrorCategory.Conflict,
        "The client relationship is not compatible with this invitation."
    );

    public static readonly Error InvalidCredentials = Error.Create(
        "authentication_invalid_credentials",
        ErrorCategory.Unauthorized,
        "The supplied credentials are invalid."
    );

    public static readonly Error RefreshSessionInvalid = Error.Create(
        "authentication_refresh_session_invalid",
        ErrorCategory.Unauthorized,
        "The refresh session is invalid or has expired."
    );

    public static readonly Error AuthenticatedAccountRequired = Error.Create(
        "authentication_account_required",
        ErrorCategory.Forbidden,
        "An authenticated platform account is required."
    );

    public static readonly Error CurrentPasswordInvalid = Error.Create(
        "authentication_current_password_invalid",
        ErrorCategory.Unauthorized,
        "The current password is invalid."
    );

    public static Error NewPasswordRejected() => Error.Validation([
        new ValidationError(
            "NewPassword",
            "authentication_new_password_rejected",
            "The new password does not satisfy the account policy."
        )
    ]);

    public static readonly Error PasswordResetInvalid = Error.Create(
        "authentication_password_reset_invalid",
        ErrorCategory.Unauthorized,
        "The password reset token is invalid or has expired."
    );

    public static readonly Error CsrfTokenInvalid = Error.Create(
        "authentication_csrf_token_invalid",
        ErrorCategory.Forbidden,
        "The anti-CSRF token is missing or invalid."
    );

    public static readonly Error OriginRejected = Error.Create(
        "authentication_origin_rejected",
        ErrorCategory.Forbidden,
        "The request origin is not allowed."
    );
}
