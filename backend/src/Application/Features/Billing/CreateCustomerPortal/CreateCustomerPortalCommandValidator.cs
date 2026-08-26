using FluentValidation;

namespace Application.Features.Billing.CreateCustomerPortal;

/// <summary>Valida identidade lógica e URL de regresso.</summary>
public sealed class CreateCustomerPortalCommandValidator
    : AbstractValidator<CreateCustomerPortalCommand>
{
    public CreateCustomerPortalCommandValidator()
    {
        RuleFor(command => command.OperationId)
            .NotEmpty()
            .WithErrorCode("billing_operation_id_required");

        RuleFor(command => command.ReturnUrl)
            .Must(command => command is not null && command.IsAbsoluteUri &&
                command.Scheme == Uri.UriSchemeHttps)
            .WithErrorCode("billing_return_url_invalid");
    }
}
