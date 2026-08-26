using FluentValidation;

namespace Application.Features.Billing.CreateCheckout;

/// <summary>Valida a forma e tiers locais publicados.</summary>
public sealed class CreateCheckoutCommandValidator
    : AbstractValidator<CreateCheckoutCommand>
{
    public CreateCheckoutCommandValidator()
    {
        RuleFor(command => command.OperationId)
            .NotEmpty()
            .WithErrorCode("billing_operation_id_required");

        RuleFor(command => command.Tier)
            .Must(command => command is "STARTER" or "PRO")
            .WithErrorCode("billing_tier_invalid");

        RuleFor(command => command.SuccessUrl)
            .Must(IsHttps)
            .WithErrorCode("billing_success_url_invalid");

        RuleFor(command => command.CancelUrl)
            .Must(IsHttps)
            .WithErrorCode("billing_cancel_url_invalid");
    }

    private static bool IsHttps(Uri url) => url is not null && url.IsAbsoluteUri &&
        url.Scheme == Uri.UriSchemeHttps;
}
