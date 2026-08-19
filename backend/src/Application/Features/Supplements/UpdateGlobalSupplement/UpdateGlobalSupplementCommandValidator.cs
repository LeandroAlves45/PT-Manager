using FluentValidation;

namespace Application.Features.Supplements.UpdateGlobalSupplement;

/// <summary>Valida a atualização de um suplemento global.</summary>
public sealed class UpdateGlobalSupplementCommandValidator
    : AbstractValidator<UpdateGlobalSupplementCommand>
{
    public UpdateGlobalSupplementCommandValidator()
    {
        RuleFor(command => command.SupplementId)
            .NotEmpty()
            .WithErrorCode("supplement_id_required");

        RuleFor(command => command.Name)
            .NotEmpty().WithErrorCode("supplement_name_required")
            .MaximumLength(255).WithErrorCode("supplement_name_too_long");

        RuleFor(command => command.UnitOfMeasure)
            .NotEmpty().WithErrorCode("supplement_unit_required")
            .MaximumLength(50).WithErrorCode("supplement_unit_too_long");

        RuleFor(command => command.ServingSize)
            .NotEmpty().WithErrorCode("supplement_serving_size_required")
            .MaximumLength(100).WithErrorCode("supplement_serving_size_too_long");

        RuleFor(command => command.Timing)
            .NotEmpty().WithErrorCode("supplement_timing_required")
            .MaximumLength(255).WithErrorCode("supplement_timing_too_long");
    }
}
