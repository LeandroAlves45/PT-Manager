using Domain.Entities.Supplements;

namespace Application.Features.Supplements.Abstractions;

/// <summary>Representa outcomes esperados da administração global.</summary>
public sealed class GlobalSupplementStoreResult
{
    public enum Status
    {
        Created,
        Updated,
        Changed,
        Deleted,
        AlreadyInRequestedState,
        NotFound,
        HasReferences,
        Inactive
    }

    public Status Kind { get; }
    public Supplement? Supplement { get; }

    private GlobalSupplementStoreResult(Status kind, Supplement? supplement)
    {
        Kind = kind;
        Supplement = supplement;
    }

    public static GlobalSupplementStoreResult WithSupplement(Status kind, Supplement supplement)
    {
        ArgumentNullException.ThrowIfNull(supplement);
        return new GlobalSupplementStoreResult(kind, supplement);
    }

    public static GlobalSupplementStoreResult For(Status kind) => new(kind, null);
}
