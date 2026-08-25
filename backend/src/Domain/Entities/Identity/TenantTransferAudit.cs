using Domain.Exceptions;

namespace Domain.Entities.Identity;

/// <summary>Registo imutável de uma transferência de conta entre tenants.</summary>
public sealed class TenantTransferAudit
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid SourceTrainerId { get; private set; }
    public Guid TargetTrainerId { get; private set; }
    public Guid TargetClientId { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private TenantTransferAudit() { }

    /// <summary>Cria um registo append-only de uma transferência concluída.</summary>
    public TenantTransferAudit(
        Guid userId,
        Guid sourceTrainerId,
        Guid targetTrainerId,
        Guid targetClientId,
        DateTime now)
    {
        if (userId == Guid.Empty ||
            sourceTrainerId == Guid.Empty ||
            targetTrainerId == Guid.Empty ||
            targetClientId == Guid.Empty)
            throw new DomainException("All transfer identifiers are required.");

        if (sourceTrainerId == targetTrainerId)
            throw new DomainException("Source and target tenants must differ.");

        Id = Guid.NewGuid();
        UserId = userId;
        SourceTrainerId = sourceTrainerId;
        TargetTrainerId = targetTrainerId;
        TargetClientId = targetClientId;
        OccurredAt = now;
    }
}

