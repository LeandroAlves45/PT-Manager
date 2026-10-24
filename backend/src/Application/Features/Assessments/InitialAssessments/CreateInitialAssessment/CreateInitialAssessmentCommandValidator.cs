using FluentValidation;

namespace Application.Features.Assessments.InitialAssessments.CreateInitialAssessment;

/// <summary>Valida a criação de uma avaliação inicial.</summary>
public sealed class CreateInitialAssessmentCommandValidator
    : AbstractValidator<CreateInitialAssessmentCommand>
{
    public CreateInitialAssessmentCommandValidator()
    {
        RuleFor(command => command.ClientId)
            .NotEmpty()
            .WithErrorCode("client_id_required");

        Include(new InitialAssessmentValuesValidator<CreateInitialAssessmentCommand>());
    }
}
