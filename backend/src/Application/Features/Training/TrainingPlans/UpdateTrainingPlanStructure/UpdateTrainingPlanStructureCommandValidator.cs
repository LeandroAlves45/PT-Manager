using FluentValidation;

namespace Application.Features.Training.TrainingPlans.UpdateTrainingPlanStructure;

/// <summary>Valida a estrutura final de um plano de treino.</summary>
public sealed class UpdateTrainingPlanStructureCommandValidator
    : AbstractValidator<UpdateTrainingPlanStructureCommand>
{
    public UpdateTrainingPlanStructureCommandValidator()
    {
        RuleFor(command => command.TrainingPlanId)
            .NotEmpty()
            .WithErrorCode("training_plan_id_required");
        RuleFor(command => command.Structure)
            .NotNull()
            .WithErrorCode("training_structure_required")
            .SetValidator(new TrainingPlanStructureValidator(false));
    }
}
