using Application.Features.Clients;
using Application.Features.Sessions.Abstractions;
using Application.Features.Sessions.Dtos;
using Application.Results;

namespace Application.Features.Sessions;

/// <summary>Mapeia outcomes de Session para o contrato estável da Application.</summary>
internal static class SessionStoreResultMapper
{
    internal static Result<SessionDto> ToResult(this SessionStoreResult outcome) =>
        outcome.Kind switch
        {
            SessionStoreResult.Status.Created or
            SessionStoreResult.Status.Updated or
            SessionStoreResult.Status.AlreadyInRequestedState =>
                Result<SessionDto>.Success(outcome.Session!.ToDto()),

            SessionStoreResult.Status.SessionNotFound =>
                Result<SessionDto>.Failure(SessionErrors.SessionNotFound),

            SessionStoreResult.Status.ClientNotFound =>
                Result<SessionDto>.Failure(ClientErrors.ClientNotFound),

            SessionStoreResult.Status.ClientInactive =>
                Result<SessionDto>.Failure(SessionErrors.ClientInactive),

            SessionStoreResult.Status.PackNotAvailable =>
                Result<SessionDto>.Failure(SessionErrors.PackNotAvailable),

            SessionStoreResult.Status.ClientDayConflict =>
                Result<SessionDto>.Failure(SessionErrors.ClientDayConflict),

            SessionStoreResult.Status.TrainerScheduleConflict =>
                Result<SessionDto>.Failure(SessionErrors.TrainerScheduleConflict),

            SessionStoreResult.Status.InvalidState =>
                Result<SessionDto>.Failure(SessionErrors.InvalidState),

            SessionStoreResult.Status.PackBalanceUnavailable =>
                Result<SessionDto>.Failure(SessionErrors.PackBalanceUnavailable),

            SessionStoreResult.Status.TransitionTooEarly =>
                Result<SessionDto>.Failure(SessionErrors.TransitionTooEarly),

            SessionStoreResult.Status.StartsAtNotFuture =>
                Result<SessionDto>.Failure(SessionErrors.StartsAtNotFuture),

            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
}
