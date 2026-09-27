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

    /// <summary>Nome do cliente do pack; presente sempre que <see cref="Pack"/> está.</summary>
    public string? ClientName { get; }

    private ClientSessionPackStoreResult(Status kind, ClientSessionPack? pack, string? clientName)
    {
        Kind = kind;
        Pack = pack;
        ClientName = clientName;
    }

    public static ClientSessionPackStoreResult ForAssigned(ClientSessionPack pack, string clientName) =>
        WithPack(Status.Assigned, pack, clientName);

    public static ClientSessionPackStoreResult ForUpdated(ClientSessionPack pack, string clientName) =>
        WithPack(Status.Updated, pack, clientName);

    public static ClientSessionPackStoreResult ForAlreadyInRequested(
        ClientSessionPack pack,
        string clientName) =>
        WithPack(Status.AlreadyInRequestedState, pack, clientName);

    public static ClientSessionPackStoreResult For(Status status) =>
        new(status, null, null);

    private static ClientSessionPackStoreResult WithPack(
        Status status,
        ClientSessionPack pack,
        string clientName
    )
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);
        return new ClientSessionPackStoreResult(status, pack, clientName);
    }
}
