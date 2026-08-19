using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Results;

namespace Application.Features.Supplements;

/// <summary>Traduz outcomes do catálogo privado para Result.</summary>
internal static class SupplementStoreResultMapper
{
    internal static Result<SupplementDto> ToDtoResult(this SupplementStoreResult outcome) =>
        outcome.Kind switch
        {
            SupplementStoreResult.Status.Created or
            SupplementStoreResult.Status.Updated =>
                Result<SupplementDto>.Success(outcome.Supplement!.ToDto()),
            SupplementStoreResult.Status.NotFound =>
                Result<SupplementDto>.Failure(SupplementErrors.SupplementNotFound),
            SupplementStoreResult.Status.GlobalReadOnly =>
                Result<SupplementDto>.Failure(SupplementErrors.GlobalSupplementReadOnly),
            SupplementStoreResult.Status.Inactive =>
                Result<SupplementDto>.Failure(SupplementErrors.SupplementInactive),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };

    internal static Result ToTransitionResult(this SupplementStoreResult outcome) =>
        outcome.Kind switch
        {
            SupplementStoreResult.Status.Changed or
            SupplementStoreResult.Status.AlreadyInRequestedState =>
                Result.Success(),
            SupplementStoreResult.Status.NotFound =>
                Result.Failure(SupplementErrors.SupplementNotFound),
            SupplementStoreResult.Status.GlobalReadOnly =>
                Result.Failure(SupplementErrors.GlobalSupplementReadOnly),
            SupplementStoreResult.Status.Inactive =>
                Result.Failure(SupplementErrors.SupplementInactive),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
}
