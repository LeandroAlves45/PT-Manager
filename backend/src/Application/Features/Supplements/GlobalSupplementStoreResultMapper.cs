using Application.Features.Supplements.Abstractions;
using Application.Features.Supplements.Dtos;
using Application.Results;

namespace Application.Features.Supplements;

/// <summary>Traduz outcomes administrativos para Result.</summary>
internal static class GlobalSupplementStoreResultMapper
{
    internal static Result<GlobalSupplementDto> ToDtoResult(
        this GlobalSupplementStoreResult outcome
    ) =>
        outcome.Kind switch
        {
            GlobalSupplementStoreResult.Status.Created or
            GlobalSupplementStoreResult.Status.Updated =>
                Result<GlobalSupplementDto>.Success(outcome.Supplement!.ToGlobalDto()),
            GlobalSupplementStoreResult.Status.NotFound =>
                Result<GlobalSupplementDto>.Failure(SupplementErrors.SupplementNotFound),
            GlobalSupplementStoreResult.Status.Inactive =>
                Result<GlobalSupplementDto>.Failure(SupplementErrors.SupplementInactive),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };

    internal static Result ToTransitionResult(this GlobalSupplementStoreResult outcome) =>
        outcome.Kind switch
        {
            GlobalSupplementStoreResult.Status.Changed or
            GlobalSupplementStoreResult.Status.Deleted or
            GlobalSupplementStoreResult.Status.AlreadyInRequestedState => Result.Success(),
            GlobalSupplementStoreResult.Status.NotFound =>
                Result.Failure(SupplementErrors.SupplementNotFound),
            GlobalSupplementStoreResult.Status.HasReferences =>
                Result.Failure(SupplementErrors.GlobalSupplementHasReferences),
            GlobalSupplementStoreResult.Status.Inactive =>
                Result.Failure(SupplementErrors.SupplementInactive),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
}
