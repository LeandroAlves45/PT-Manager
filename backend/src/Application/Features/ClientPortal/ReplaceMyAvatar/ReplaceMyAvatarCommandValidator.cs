using Application.Common.Abstractions;
using FluentValidation;

namespace Application.Features.ClientPortal.ReplaceMyAvatar;

/// <summary>Valida presença, Content-Type declarado e tamanho declarado.</summary>
public sealed class ReplaceMyAvatarCommandValidator : AbstractValidator<ReplaceMyAvatarCommand>
{
    public ReplaceMyAvatarCommandValidator()
    {
        RuleFor(command => command.Avatar)
            .NotNull()
            .WithErrorCode("portal_avatar_required");

        RuleFor(command => command.Avatar!.ContentType)
            .Must(ImageProfiles.AcceptedContentTypes.Contains)
            .WithErrorCode("portal_avatar_unsupported_media_type")
            .When(command => command.Avatar is not null);

        RuleFor(command => command.Avatar!.LengthInBytes)
            .InclusiveBetween(1, ImageProfiles.ClientAvatar.MaxBytes)
            .WithErrorCode("portal_avatar_media_too_large")
            .When(command => command.Avatar is not null);
    }
}
