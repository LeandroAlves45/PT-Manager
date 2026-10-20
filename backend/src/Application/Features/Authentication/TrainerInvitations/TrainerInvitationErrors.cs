using Application.Errors;

namespace Application.Features.Authentication.TrainerInvitations;

/// <summary>
/// Erros do convite isento. O inválido não diz porquê: um link anulado, substituído ou inventado
/// responde igual, para não revelar se o email existe (RN-008).
/// </summary>
public static class TrainerInvitationErrors
{
    public static readonly Error Invalid = Error.Create(
        "trainer_invitation_invalid",
        ErrorCategory.Unauthorized,
        "The trainer invitation is invalid.");

    public static readonly Error Expired = Error.Create(
        "trainer_invitation_expired",
        ErrorCategory.Unauthorized,
        "The trainer invitation has expired.");

    public static readonly Error Used = Error.Create(
        "trainer_invitation_used",
        ErrorCategory.Conflict,
        "The trainer invitation was already used.");
}
