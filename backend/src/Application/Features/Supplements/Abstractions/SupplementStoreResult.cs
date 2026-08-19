using Domain.Entities.Supplements;

namespace Application.Features.Supplements.Abstractions;

/// <summary>Representa outcomes esperados do store de suplementos privados.</summary>
public sealed class SupplementStoreResult
{
    public enum Status
    {
        Created,
        Updated,
        Changed,
        AlreadyInRequestedState,
        NotFound,
        GlobalReadOnly,
        Inactive
    }

    public Status Kind { get; }
    public Supplement? Supplement { get; }

    private SupplementStoreResult(Status kind, Supplement? supplement)
    {
        Kind = kind;
        Supplement = supplement;
    }

    public static SupplementStoreResult WithSupplement(Status kind, Supplement supplement)
    {
        ArgumentNullException.ThrowIfNull(supplement);
        return new SupplementStoreResult(kind, supplement);
    }

    public static SupplementStoreResult For(Status kind) => new(kind, null);
}
