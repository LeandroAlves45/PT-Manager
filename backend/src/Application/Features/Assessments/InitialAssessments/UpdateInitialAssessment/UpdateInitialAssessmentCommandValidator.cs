using FluentValidation;

namespace Application.Features.Assessments.InitialAssessments.UpdateInitialAssessment;

/// <summary>Valida uma correção total de uma avaliação inicial.</summary>
public sealed class UpdateInitialAssessmentCommandValidator
    : AbstractValidator<UpdateInitialAssessmentCommand>
{
    public UpdateInitialAssessmentCommandValidator()
    {
        RuleFor(command => command.AssessmentId)
            .NotEmpty().WithErrorCode("initial_assessment_id_required");

        Include(new InitialAssessmentValuesValidator<UpdateInitialAssessmentCommand>());
    }
}
