using System.Text.RegularExpressions;
using FluentValidation;

namespace Application.Features.TrainerSettings.UpdateBranding;

/// <summary>Valida AppName e o formato hexadecimal das cores opcionais.</summary>
public sealed partial class UpdateBrandingCommandValidator
    : AbstractValidator<UpdateBrandingCommand>
{
    public UpdateBrandingCommandValidator()
    {
        RuleFor(command => command.AppName)
            .NotEmpty()
            .WithErrorCode("trainer_settings_app_name_required")
            .Length(2, 50)
            .WithErrorCode("trainer_settings_app_name_length");

        RuleFor(command => command.PrimaryColor)
            .Must(BeAValidHexColor)
            .When(command => command.PrimaryColor is not null)
            .WithErrorCode("trainer_settings_primary_color_invalid");

        RuleFor(command => command.BodyColor)
            .Must(BeAValidHexColor)
            .When(command => command.BodyColor is not null)
            .WithErrorCode("trainer_settings_body_color_invalid");
    }

    private static bool BeAValidHexColor(string? color) =>
        color is not null && HexColorPattern().IsMatch(color);

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorPattern();
}

