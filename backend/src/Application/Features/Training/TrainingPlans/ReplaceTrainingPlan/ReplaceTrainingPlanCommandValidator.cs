using FluentValidation;

namespace Application.Features.Training.TrainingPlans.ReplaceTrainingPlan;

/// <summary>Valida metadados e estrutura de replacement.</summary>
public sealed class ReplaceTrainingPlanCommandValidator
    : AbstractValidator<ReplaceTrainingPlanCommand>
{
    public ReplaceTrainingPlanCommandValidator()
    {
        RuleFor(command => command.TrainingPlanId)
            .NotEmpty()
            .WithErrorCode("training_plan_id_required");
        RuleFor(command => command.Name)
            .NotEmpty()
            .WithErrorCode("training_plan_name_required")
            .MaximumLength(255)
            .WithErrorCode("training_plan_name_too_long");
        RuleFor(command => command.TrainingModality)
            .MaximumLength(50)
            .WithErrorCode("training_modality_too_long");
        RuleFor(command => command)
            .Must(command => !command.EndDate.HasValue ||
                command.EndDate.Value >= command.StartDate)
            .WithName("EndDate")
            .WithErrorCode("training_plan_date_range_invalid");
        RuleFor(command => command.Structure)
            .NotNull()
            .WithErrorCode("training_structure_required")
            .SetValidator(new TrainingPlanStructureValidator(true));
    }
}
