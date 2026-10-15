using Domain.ValueObjects;
using FluentValidation;

namespace Application.Features.Authentication.RequestEmailConfirmation;

/// <summary>Valida o formato do email com a mesma regra do value object.</summary>
public sealed class RequestEmailConfirmationCommandValidator
    : AbstractValidator<RequestEmailConfirmationCommand>
{
    public RequestEmailConfirmationCommandValidator()
    {
        RuleFor(command => command.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("authentication_email_required")
            .Must(EmailAddress.IsValid)
            .WithErrorCode("authentication_email_invalid");
    }
}
