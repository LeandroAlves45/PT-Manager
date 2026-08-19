using FluentValidation;

namespace Application.Features.Supplements.UpdateSupplementAssignment;

/// <summary>Valida a atualização das instruções prescritas.</summary>
public sealed class UpdateSupplementAssignmentCommandValidator
    : AbstractValidator<UpdateSupplementAssignmentCommand>
{
    public UpdateSupplementAssignmentCommandValidator()
    {
        RuleFor(command => command.AssignmentId).NotEmpty()
            .WithErrorCode("supplement_assignment_id_required");

        RuleFor(command => command.ServingSize).NotEmpty()
            .WithErrorCode("supplement_assignment_serving_size_required")
            .MaximumLength(100).WithErrorCode("supplement_assignment_serving_size_too_long");

        RuleFor(command => command.Timing).NotEmpty()
            .WithErrorCode("supplement_assignment_timing_required")
            .MaximumLength(255).WithErrorCode("supplement_assignment_timing_too_long");
    }
}
