using FluentValidation;

namespace Application.Features.Supplements.AssignSupplement;

/// <summary>Valida uma atribuição sem exigir os valores que podem vir do catálogo.</summary>
public sealed class AssignSupplementCommandValidator : AbstractValidator<AssignSupplementCommand>
{
    public AssignSupplementCommandValidator()
    {
        RuleFor(command => command.ClientId)
            .NotEmpty().WithErrorCode("client_id_required");

        RuleFor(command => command.SupplementId)
            .NotEmpty().WithErrorCode("supplement_id_required");

        RuleFor(command => command.ServingSize)
            .Must(value => value is null || !string.IsNullOrWhiteSpace(value))
            .WithErrorCode("supplement_assignment_serving_size_invalid")
            .MaximumLength(100).WithErrorCode("supplement_assignment_serving_size_too_long");

        RuleFor(command => command.Timing)
            .Must(value => value is null || !string.IsNullOrWhiteSpace(value))
            .WithErrorCode("supplement_assignment_timing_invalid")
            .MaximumLength(255).WithErrorCode("supplement_assignment_timing_too_long");
    }
}
