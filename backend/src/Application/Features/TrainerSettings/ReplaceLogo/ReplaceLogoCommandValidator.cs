using Application.Common.Abstractions;
using FluentValidation;

namespace Application.Features.TrainerSettings.ReplaceLogo;

/// <summary>
/// Valida o que é conhecido sem I/O: presença, Content-Type declarado e tamanho declarado.
/// </summary>
public sealed class ReplaceLogoCommandValidator : AbstractValidator<ReplaceLogoCommand>
{
    public ReplaceLogoCommandValidator()
    {
        RuleFor(command => command.Logo)
            .NotNull()
            .WithErrorCode("trainer_settings_logo_required");

        RuleFor(command => command.Logo!.ContentType)
            .Must(ImageProfiles.AcceptedContentTypes.Contains)
            .WithErrorCode("trainer_settings_unsupported_media_type")
            .When(command => command.Logo is not null);

        RuleFor(command => command.Logo!.LengthInBytes)
            .InclusiveBetween(1, ImageProfiles.TrainerLogo.MaxBytes)
            .WithErrorCode("trainer_settings_media_too_large")
            .When(command => command.Logo is not null);
    }
}
