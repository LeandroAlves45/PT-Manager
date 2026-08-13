using FluentValidation;

namespace Application.Features.Training.TrainingPlans.UpdateTrainingPlanMetadata;

/// <summary>Valida metadados editáveis de um plano de treino.</summary>
public sealed class UpdateTrainingPlanMetadataCommandValidator
    : AbstractValidator<UpdateTrainingPlanMetadataCommand>
{
    public UpdateTrainingPlanMetadataCommandValidator()
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
    }
}
