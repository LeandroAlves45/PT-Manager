using Application.Features.Assessments.InitialAssessments;
using FluentValidation;

namespace Application.Features.ClientPortal.SubmitMyInitialAssessment;

/// <summary>Valida a avaliação inicial do cliente com as mesmas regras do personal trainer.</summary>
public sealed class SubmitMyInitialAssessmentCommandValidator
    : AbstractValidator<SubmitMyInitialAssessmentCommand>
{
    public SubmitMyInitialAssessmentCommandValidator() =>
        Include(new InitialAssessmentValuesValidator<SubmitMyInitialAssessmentCommand>());
}
