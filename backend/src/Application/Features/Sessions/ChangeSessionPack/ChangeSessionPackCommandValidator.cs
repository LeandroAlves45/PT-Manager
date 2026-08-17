using FluentValidation;

namespace Application.Features.Sessions.ChangeSessionPack;

/// <summary>Valida a troca explícita de pack.</summary>
public sealed class ChangeSessionPackCommandValidator : AbstractValidator<ChangeSessionPackCommand>
{
    public ChangeSessionPackCommandValidator()
    {
        RuleFor(command => command.SessionId)
            .NotEmpty().WithErrorCode("session_id_required");

        RuleFor(command => command.ClientSessionPackId)
            .Must(id => !id.HasValue || id.Value != Guid.Empty)
            .WithErrorCode("client_session_pack_id_invalid");
    }
}
