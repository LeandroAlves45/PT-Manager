namespace Application.Features.Packs.ClientSessionPacks.Abstractions;

/// <summary>Persiste mutações tenant-safe de packs atribuídos.</summary>
public interface IClientSessionPackStore
{
    Task<ClientSessionPackStoreResult> AssignAsync(
        Guid trainerId,
        Guid clientId,
        Guid packTypeId,
        DateOnly purchaseDate,
        DateOnly? expectedEndDate,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<ClientSessionPackStoreResult> UpdateExpectedEndDateAsync(
        Guid trainerId,
        Guid packId,
        DateOnly? expectedEndDate,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<ClientSessionPackStoreResult> CancelAsync(
        Guid trainerId,
        Guid packId,
        DateTime now,
        CancellationToken cancellationToken
    );
}
