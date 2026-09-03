using Application.Features.ClientPortal.Dtos;

namespace Application.Features.ClientPortal.Abstractions;

/// <summary>Desfecho da escrita do perfil do próprio cliente.</summary>
public sealed record UpdateMyProfileOutcome(
    UpdateMyProfileStatus Status,
    MyProfileDto? Profile)
{
    public static UpdateMyProfileOutcome Updated(MyProfileDto profile) =>
        new(UpdateMyProfileStatus.Updated, profile);

    public static readonly UpdateMyProfileOutcome NotFound =
        new(UpdateMyProfileStatus.NotFound, null);

    public static readonly UpdateMyProfileOutcome DuplicateEmail =
        new(UpdateMyProfileStatus.DuplicateEmail, null);

    public static readonly UpdateMyProfileOutcome DuplicatePhone =
        new(UpdateMyProfileStatus.DuplicatePhone, null);
}
