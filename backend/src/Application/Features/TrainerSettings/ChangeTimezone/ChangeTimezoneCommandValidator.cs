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

    private static bool BeAKnownIanaTimezone(string timezone) =>
        !string.IsNullOrWhiteSpace(timezone) &&
        TimeZoneInfo.TryFindSystemTimeZoneById(timezone.Trim(), out _);
}
