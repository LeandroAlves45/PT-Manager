using Domain.Entities.Billing;

namespace Application.Features.Packs.ClientSessionPacks.Abstractions;

/// <summary>Resultado esperado de uma mutação de ClientSessionPack.</summary>
public sealed class ClientSessionPackStoreResult
{
    public enum Status
    {
        Assigned,
        Updated,
        Cancelled,
        ClientNotFound,
        ClientInactive,
        AlreadyInRequestedState,
        PackTypeNotFound,
        PackTypeInactive,
        PackNotFound,
        ExpectedEndDateBeforePurchase,
        PackUsed,
        PackReferenced
    }

    public Status Kind { get; }
    public ClientSessionPack? Pack { get; }

    private ClientSessionPackStoreResult(Status kind, ClientSessionPack? pack)
    {
        Kind = kind;
        Pack = pack;
    }

    public static ClientSessionPackStoreResult ForAssigned(ClientSessionPack pack) =>
        WithPack(Status.Assigned, pack);

    public static ClientSessionPackStoreResult ForUpdated(ClientSessionPack pack) =>
        WithPack(Status.Updated, pack);

    public static ClientSessionPackStoreResult ForAlreadyInRequested(ClientSessionPack pack) =>
        WithPack(Status.AlreadyInRequestedState, pack);

    public static ClientSessionPackStoreResult For(Status status) =>
        new(status, null);

    private static ClientSessionPackStoreResult WithPack(
        Status status,
        ClientSessionPack pack
    )
    {
        ArgumentNullException.ThrowIfNull(pack);
        return new ClientSessionPackStoreResult(status, pack);
    }
}
