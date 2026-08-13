using FluentValidation;

namespace Application.Features.Training.TrainingPlans.CreateTrainingPlan;

/// <summary>Valida a criação integral de um plano de treino.</summary>
public sealed class CreateTrainingPlanCommandValidator : AbstractValidator<CreateTrainingPlanCommand>
{
    public CreateTrainingPlanCommandValidator()
    {
        RuleFor(command => command.ClientId)
            .NotEmpty()
            .WithErrorCode("training_client_id_required");
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
