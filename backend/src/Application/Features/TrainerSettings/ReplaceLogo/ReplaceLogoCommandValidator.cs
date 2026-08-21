using FluentValidation;

namespace Application.Features.TrainerSettings.ReplaceLogo;

/// <summary>Valida o formato e tamanho do logo antes de qualquer upload externo.</summary>
public sealed class ReplaceLogoCommandValidator : AbstractValidator<ReplaceLogoCommand>
{
    private const long MaxLogoBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/webp"
    };

    public ReplaceLogoCommandValidator()
    {
        RuleFor(command => command.Logo.ContentType)
            .Must(contentType => AllowedContentTypes.Contains(contentType))
            .WithErrorCode("trainer_settings_unsupported_media_type");

        RuleFor(command => command.Logo.LengthInBytes)
            .InclusiveBetween(1, MaxLogoBytes)
            .WithErrorCode("trainer_settings_media_too_large");
    }
}
