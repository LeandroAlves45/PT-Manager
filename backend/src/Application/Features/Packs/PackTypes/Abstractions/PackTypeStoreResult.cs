using Domain.Entities.Billing;

namespace Application.Features.Packs.PackTypes.Abstractions;

/// <summary>Resultado esperado de uma mutação de um tipo de pack.</summary>
public sealed class PackTypeStoreResult
{
    public enum Status
    {
        Updated,
        Changed,
        AlreadyInRequestedState,
        NotFound
    }

    public Status Kind { get; }
    public PackType? PackType { get; }

    private PackTypeStoreResult(Status kind, PackType? packType)
    {
        Kind = kind;
        PackType = packType;
    }

    public static PackTypeStoreResult ForUpdated(PackType packType)
    {
        ArgumentNullException.ThrowIfNull(packType);
        return new PackTypeStoreResult(Status.Updated, packType);
    }

    public static PackTypeStoreResult ForChanged() =>
        new(Status.Changed, null);
    public static PackTypeStoreResult ForAlreadyInRequested() =>
        new(Status.AlreadyInRequestedState, null);
    public static PackTypeStoreResult ForNotFound() =>
        new(Status.NotFound, null);
}
