using FluentValidation;

namespace Application.Features.Packs.PackTypes.CreatePackType;

/// <summary>Valida a forma de CreatePackTypeCommand.</summary>
public sealed class CreatePackTypeCommandValidator : AbstractValidator<CreatePackTypeCommand>
{
    public CreatePackTypeCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .WithErrorCode("pack_type_name_required")
            .MaximumLength(255)
            .WithErrorCode("pack_type_name_too_long");

        RuleFor(command => command.SessionCount)
            .GreaterThan(0)
            .WithErrorCode("pack_type_session_count_must_be_positive");

        RuleFor(command => command.PriceCents)
            .GreaterThanOrEqualTo(0)
            .WithErrorCode("pack_type_price_non_negative");

        RuleFor(command => command.Currency)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithErrorCode("pack_type_currency_required")
            .Length(3)
            .WithErrorCode("pack_type_currency_invalid")
            .Matches("^[A-Za-z]{3}$")
            .WithErrorCode("pack_type_currency_invalid");

        RuleFor(command => command.ExpectedDurationDays)
            .GreaterThan(0)
            .When(command => command.ExpectedDurationDays.HasValue)
            .WithErrorCode("pack_type_expected_duration_must_be_positive");
    }
}
