using Application.Features.Authentication.TrainerInvitations;

namespace Api.Contracts.Authentication;

public sealed record TrainerInvitationTokenRequest(string? Token);

public sealed record InvitedTrainerRegistrationRequest(
    string? Token,
    string? FullName,
    string? Password,
    string? ConfirmPassword);

/// <summary><c>status</c> é <c>valid</c>, <c>expired</c>, <c>used</c> ou <c>invalid</c>.</summary>
public sealed record TrainerInvitationStatusResponse(string Status, string? Email)
{
    public static TrainerInvitationStatusResponse From(TrainerInvitationStatusResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new TrainerInvitationStatusResponse(
            result.Status switch
            {
                TrainerInvitationStatus.Valid => "valid",
                TrainerInvitationStatus.Expired => "expired",
                TrainerInvitationStatus.Used => "used",
                _ => "invalid"
            },
            result.Email);
    }
}
