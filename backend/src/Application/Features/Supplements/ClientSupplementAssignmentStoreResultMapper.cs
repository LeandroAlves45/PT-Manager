using Application.Features.Clients;
using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Results;

namespace Application.Features.Supplements;

/// <summary>Traduz outcomes de atribuições de suplementos para Result.</summary>
internal static class ClientSupplementAssignmentStoreResultMapper
{
    internal static Result<ClientSupplementAssignmentDto> ToDtoResult(
        this ClientSupplementAssignmentStoreResult outcome
    ) =>
        outcome.Kind switch
        {
            ClientSupplementAssignmentStoreResult.Status.Assigned or
            ClientSupplementAssignmentStoreResult.Status.Updated or
            ClientSupplementAssignmentStoreResult.Status.Changed or
            ClientSupplementAssignmentStoreResult.Status.AlreadyInRequestedState =>
                Result<ClientSupplementAssignmentDto>.Success(
                    outcome.Assignment!.ToDto(outcome.Supplement!)
                ),
            ClientSupplementAssignmentStoreResult.Status.ClientNotFound =>
                Result<ClientSupplementAssignmentDto>.Failure(ClientErrors.ClientNotFound),
            ClientSupplementAssignmentStoreResult.Status.ClientInactive =>
                Result<ClientSupplementAssignmentDto>.Failure(SupplementErrors.ClientInactive),
            ClientSupplementAssignmentStoreResult.Status.SupplementNotFound =>
                Result<ClientSupplementAssignmentDto>.Failure(SupplementErrors.SupplementNotFound),
            ClientSupplementAssignmentStoreResult.Status.SupplementInactive =>
                Result<ClientSupplementAssignmentDto>.Failure(SupplementErrors.SupplementInactive),
            ClientSupplementAssignmentStoreResult.Status.AssignmentNotFound =>
                Result<ClientSupplementAssignmentDto>.Failure(SupplementErrors.AssignmentNotFound),
            ClientSupplementAssignmentStoreResult.Status.AssignmentAlreadyExists =>
                Result<ClientSupplementAssignmentDto>.Failure(SupplementErrors.AssignmentAlreadyExists),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
}
