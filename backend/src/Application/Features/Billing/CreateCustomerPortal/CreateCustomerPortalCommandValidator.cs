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
    }
}
