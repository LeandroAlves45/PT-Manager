using FluentValidation;

namespace Application.Features.TrainerSettings.ChangeTimezone;

/// <summary>Valida que o timezone é um identificador IANA reconhecido.</summary>
public sealed class ChangeTimezoneCommandValidator : AbstractValidator<ChangeTimezoneCommand>
{
    public ChangeTimezoneCommandValidator()
    {
        RuleFor(command => command.Timezone)
            .NotEmpty()
            .WithErrorCode("trainer_settings_timezone_required")
            .Must(BeAKnownIanaTimezone)
            .WithErrorCode("trainer_settings_invalid_timezone")
            .WithMessage("Timezone is not a known IANA identifier.");
    }

    private static bool BeAKnownIanaTimezone(string? timezone)
    {
        var normalized = timezone?.Trim() ?? string.Empty;

        return IsValidIanaShape(normalized) &&
            TimeZoneInfo.TryFindSystemTimeZoneById(normalized, out _);
    }

    private static bool IsValidIanaShape(string value) =>
        value == "UTC" ||
        (value.Length is > 2 and <= 100 && value.Contains('/') &&
            value.All(character =>
                char.IsLetterOrDigit(character) || character is '/' or '_' or '-' or '+'));
}
