using Application.Features.ClientPortal.Dtos;

namespace Application.Features.ClientPortal.Abstractions;

/// <summary>Desfecho da escrita do avatar do próprio cliente.</summary>
public sealed record MyAvatarOutcome(
    MyAvatarStatus Status,
    MyProfileDto? Profile)
{
    public static MyAvatarOutcome Updated(MyProfileDto profile) =>
        new(MyAvatarStatus.Updated, profile);

    public static readonly MyAvatarOutcome NotFound =
        new(MyAvatarStatus.NotFound, null);
}
