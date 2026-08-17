using Application.Features.Packs.PackTypes.Abstractions;
using Application.Features.Packs.PackTypes.Dtos;
using Application.Results;

namespace Application.Features.Packs.PackTypes;

/// <summary>Converte resultados de persistência de tipos de pack em resultados da Application.</summary>
internal static class PackTypeStoreResultMapper
{
    internal static Result ToTransitionResult(this PackTypeStoreResult outcome) =>
        outcome.Kind switch
        {
            PackTypeStoreResult.Status.Changed or
            PackTypeStoreResult.Status.AlreadyInRequestedState => Result.Success(),
            PackTypeStoreResult.Status.NotFound => Result.Failure(PackErrors.PackTypeNotFound),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };

    internal static Result<PackTypeDto> ToUpdateResult(this PackTypeStoreResult outcome) =>
        outcome.Kind switch
        {
            PackTypeStoreResult.Status.Updated =>
                Result<PackTypeDto>.Success(outcome.PackType!.ToDto()),
            PackTypeStoreResult.Status.NotFound =>
                Result<PackTypeDto>.Failure(PackErrors.PackTypeNotFound),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome.Kind))
        };
}
