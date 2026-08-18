using Application.Features.Assessments.InitialAssessments.Abstractions;
using Application.Features.Assessments.InitialAssessments.Dtos;
using Application.Features.Clients;
using Application.Results;

namespace Application.Features.Assessments.InitialAssessments;

/// <summary>Traduz outcomes de InitialAssessment para Result.</summary>
internal static class InitialAssessmentStoreResultMapper
{
    internal static Result<InitialAssessmentDto> ToResult(
        this InitialAssessmentStoreResult outcome) => outcome.Kind switch
        {
            InitialAssessmentStoreResult.Status.Created or
            InitialAssessmentStoreResult.Status.Updated or
            InitialAssessmentStoreResult.Status.AlreadyInRequestedState =>
                Result<InitialAssessmentDto>.Success(outcome.Assessment!.ToDto()),

            InitialAssessmentStoreResult.Status.ClientNotFound =>
                Result<InitialAssessmentDto>.Failure(ClientErrors.ClientNotFound),

            InitialAssessmentStoreResult.Status.ClientInactive =>
                Result<InitialAssessmentDto>.Failure(AssessmentErrors.ClientInactive),

            InitialAssessmentStoreResult.Status.AssessmentNotFound =>
                Result<InitialAssessmentDto>.Failure(AssessmentErrors.InitialAssessmentNotFound),
            InitialAssessmentStoreResult.Status.AssessmentAlreadyExists =>
                Result<InitialAssessmentDto>.Failure(AssessmentErrors.InitialAssessmentAlreadyExists),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
}
