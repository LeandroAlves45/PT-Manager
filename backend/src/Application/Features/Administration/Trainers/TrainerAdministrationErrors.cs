using Application.Errors;

namespace Application.Features.Administration.Trainers;

/// <summary>Erros estáveis da gestão de treinadores pelo superuser (RN-008, RN-018).</summary>
public static class TrainerAdministrationErrors
{
    public static readonly Error AdministratorOnly = Error.Create(
        "trainer_administration_administrator_only",
        ErrorCategory.Forbidden,
        "Only an active superuser in administrative context can manage trainers.");

    public static readonly Error TrainerNotFound = Error.Create(
        "admin_trainer_not_found",
        ErrorCategory.NotFound,
        "The trainer was not found.");

    public static readonly Error EmailAlreadyRegistered = Error.Create(
        "trainer_invitation_email_registered",
        ErrorCategory.Conflict,
        "This email already has an account. Exempt the existing account instead.");

    public static readonly Error InvitationNotFound = Error.Create(
        "trainer_invitation_not_found",
        ErrorCategory.NotFound,
        "The pending trainer invitation was not found.");

    public static readonly Error PaymentProviderUnavailable = Error.Create(
        "admin_trainer_payment_provider_unavailable",
        ErrorCategory.ExternalDependency,
        "The paid subscription could not be cancelled at the payment provider. Try again.");

    public static readonly Error ConcurrencyConflict = Error.Create(
        "admin_trainer_concurrency_conflict",
        ErrorCategory.Conflict,
        "The trainer changed during the operation. Try again.");
}
