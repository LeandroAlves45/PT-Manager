using FluentValidation;

namespace Application.Features.Authentication.RequestPasswordReset;

/// <summary>Valida o pedido público de reset de password.</summary>
public sealed class RequestPasswordResetCommandValidator
    : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetCommandValidator()
    {
        RuleFor(command => command.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("authentication_email_required")
            .MaximumLength(255)
            .WithErrorCode("authentication_email_too_long")
            .EmailAddress()
            .WithErrorCode("authentication_email_invalid");
    }
}
