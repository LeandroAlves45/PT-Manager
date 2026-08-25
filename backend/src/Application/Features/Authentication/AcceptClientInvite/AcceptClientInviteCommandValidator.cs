using FluentValidation;

namespace Application.Features.Authentication.AcceptClientInvite;

/// <summary>Valida o contrato da aceitação de um convite.</summary>
public sealed class AcceptClientInviteCommandValidator
    : AbstractValidator<AcceptClientInviteCommand>
{
    public AcceptClientInviteCommandValidator()
    {
        RuleFor(command => command.Token)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("authentication_invite_token_required")
            .MaximumLength(512)
            .WithErrorCode("authentication_invite_token_invalid");
    }
}
