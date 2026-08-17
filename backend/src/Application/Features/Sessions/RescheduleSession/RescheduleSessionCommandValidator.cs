using Application.Common.Abstractions;
using FluentValidation;

namespace Application.Features.Sessions.RescheduleSession;

/// <summary>Valida um reagendamento de sessão.</summary>
public sealed class RescheduleSessionCommandValidator : AbstractValidator<RescheduleSessionCommand>
{
    public RescheduleSessionCommandValidator(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        RuleFor(command => command.SessionId)
            .NotEmpty().WithErrorCode("session_id_required");

        RuleFor(command => command.StartsAt)
            .Must(value => value != default &&
                value.ToUniversalTime() > new DateTimeOffset(clock.UtcNow, TimeSpan.Zero))
            .WithErrorCode("session_starts_at_not_future");

        RuleFor(command => command.DurationMinutes)
            .GreaterThan(0).WithErrorCode("session_duration_invalid");

        RuleFor(command => command.Location)
            .MaximumLength(255).WithErrorCode("session_location_too_long");
    }
}
