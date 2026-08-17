using Application.Features.Clients;
using Application.Features.Packs.ClientSessionPacks.Abstractions;
using Application.Features.Packs.ClientSessionPacks.Dtos;
using Application.Results;

namespace Application.Features.Packs.ClientSessionPacks;

/// <summary>Converte resultados de persistência de packs atribuídos em resultados da Application.</summary>
internal static class ClientSessionPackStoreResultMapper
{
    internal static Result<ClientSessionPackDto> ToAssignResult(
        this ClientSessionPackStoreResult outcome) =>
        outcome.Kind switch
        {
            ClientSessionPackStoreResult.Status.Assigned =>
                Result<ClientSessionPackDto>.Success(outcome.Pack!.ToDto()),
            ClientSessionPackStoreResult.Status.ClientNotFound =>
                Result<ClientSessionPackDto>.Failure(ClientErrors.ClientNotFound),
            ClientSessionPackStoreResult.Status.ClientInactive =>
                Result<ClientSessionPackDto>.Failure(ClientErrors.ClientInactive),
            ClientSessionPackStoreResult.Status.PackTypeNotFound =>
                Result<ClientSessionPackDto>.Failure(PackErrors.PackTypeNotFound),
            ClientSessionPackStoreResult.Status.PackTypeInactive =>
                Result<ClientSessionPackDto>.Failure(PackErrors.PackTypeInactive),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };

    internal static Result ToCancelResult(this ClientSessionPackStoreResult outcome) =>
        outcome.Kind switch
        {
            ClientSessionPackStoreResult.Status.Cancelled or
            ClientSessionPackStoreResult.Status.AlreadyInRequestedState => Result.Success(),
            ClientSessionPackStoreResult.Status.PackNotFound =>
                Result.Failure(PackErrors.ClientSessionPackNotFound),
            ClientSessionPackStoreResult.Status.PackUsed =>
                Result.Failure(PackErrors.ClientSessionPackUsed),
            ClientSessionPackStoreResult.Status.PackReferenced =>
                Result.Failure(PackErrors.ClientSessionPackReferenced),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };

    internal static Result<ClientSessionPackDto> ToUpdateResult(
        this ClientSessionPackStoreResult outcome) =>
        outcome.Kind switch
        {
            ClientSessionPackStoreResult.Status.Updated or
            ClientSessionPackStoreResult.Status.AlreadyInRequestedState =>
                Result<ClientSessionPackDto>.Success(outcome.Pack!.ToDto()),
            ClientSessionPackStoreResult.Status.PackNotFound =>
                Result<ClientSessionPackDto>.Failure(PackErrors.ClientSessionPackNotFound),
            ClientSessionPackStoreResult.Status.ExpectedEndDateBeforePurchase =>
                Result<ClientSessionPackDto>.Failure(PackErrors.ExpectedEndDateBeforePurchase),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
}
