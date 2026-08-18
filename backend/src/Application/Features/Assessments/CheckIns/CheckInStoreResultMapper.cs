using Application.Features.Assessments.CheckIns.Abstractions;
using Application.Features.Assessments.CheckIns.Dtos;
using Application.Features.Clients;
using Application.Results;

namespace Application.Features.Assessments.CheckIns;

/// <summary>Traduz outcomes de CheckIn para resultados da Application.</summary>
internal static class CheckInStoreResultMapper
{
    internal static Result<CheckInDto> ToResult(
        this CheckInStoreResult outcome, DateOnly localToday) => outcome.Kind switch
        {
            CheckInStoreResult.Status.Created or
            CheckInStoreResult.Status.Rescheduled or
            CheckInStoreResult.Status.Cancelled or
            CheckInStoreResult.Status.Answered or
            CheckInStoreResult.Status.Corrected or
            CheckInStoreResult.Status.AlreadyInRequestedState =>
                Result<CheckInDto>.Success(outcome.CheckIn!.ToDto(localToday)),

            CheckInStoreResult.Status.ClientNotFound =>
                Result<CheckInDto>.Failure(ClientErrors.ClientNotFound),

            CheckInStoreResult.Status.ClientInactive =>
                Result<CheckInDto>.Failure(AssessmentErrors.ClientInactive),

            CheckInStoreResult.Status.CheckInNotFound =>
                Result<CheckInDto>.Failure(AssessmentErrors.CheckInNotFound),

            CheckInStoreResult.Status.DateConflict =>
                Result<CheckInDto>.Failure(AssessmentErrors.CheckInDateConflict),

            CheckInStoreResult.Status.DateNotAllowed =>
                Result<CheckInDto>.Failure(AssessmentErrors.CheckInDateNotAllowed),

            CheckInStoreResult.Status.CannotReschedule =>
                Result<CheckInDto>.Failure(AssessmentErrors.CheckInCannotBeRescheduled),

            CheckInStoreResult.Status.CannotCancel =>
                Result<CheckInDto>.Failure(AssessmentErrors.CheckInCannotBeCancelled),

            CheckInStoreResult.Status.WrongResponseDay =>
                Result<CheckInDto>.Failure(AssessmentErrors.CheckInWrongDay),

            CheckInStoreResult.Status.AlreadyAnswered =>
                Result<CheckInDto>.Failure(AssessmentErrors.CheckInAlreadyAnswered),

            CheckInStoreResult.Status.CheckInCancelled =>
                Result<CheckInDto>.Failure(AssessmentErrors.CheckInCancelled),

            CheckInStoreResult.Status.NotAnswered =>
                Result<CheckInDto>.Failure(AssessmentErrors.CheckInNotAnswered),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
}
