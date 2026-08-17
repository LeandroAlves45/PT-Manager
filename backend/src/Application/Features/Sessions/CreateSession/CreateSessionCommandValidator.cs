using Application.Common.Abstractions;
using FluentValidation;

namespace Application.Features.Sessions.CreateSession;

/// <summary>Valida os campos necessários para criar uma sessão.</summary>
public sealed class CreateSessionCommandValidator : AbstractValidator<CreateSessionCommand>
{
    public CreateSessionCommandValidator(IClock clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        RuleFor(command => command.ClientId)
            .NotEmpty().WithErrorCode("client_id_required");

        RuleFor(command => command.ClientSessionPackId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithErrorCode("client_session_pack_id_invalid");

        RuleFor(command => command.StartsAt)
            .Must(value => value != default &&
                value.ToUniversalTime() > new DateTimeOffset(clock.UtcNow, TimeSpan.Zero))
            .WithErrorCode("session_starts_at_not_future")
            .WithMessage("Session start must be in the future.");

        RuleFor(command => command.DurationMinutes)
            .GreaterThan(0).WithErrorCode("session_duration_invalid");

        RuleFor(command => command.Location)
            .MaximumLength(255).WithErrorCode("session_location_too_long");

        RuleFor(command => command.SessionType)
            .MaximumLength(50).WithErrorCode("session_type_too_long");
    }
}
