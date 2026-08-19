using Domain.Exceptions;

namespace Domain.Entities.Administration;

/// <summary>Registo imutável de uma mutação administrativa.</summary>
public sealed class AdministrativeAuditEntry
{
    public Guid Id { get; private set; }
    public Guid ActorUserId { get; private set; }
    public string Action { get; private set; } = null!;
    public string ResourceType { get; private set; } = null!;
    public Guid ResourceId { get; private set; }
    public string? BeforeState { get; private set; }
    public string? AfterState { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private AdministrativeAuditEntry() { }

    /// <summary>Cria uma entrada que não oferece operações de alteração ou eliminação.</summary>
    public AdministrativeAuditEntry(
        Guid actorUserId,
        string action,
        string resourceType,
        Guid resourceId,
        string? beforeState,
        string? afterState,
        DateTime occurredAt
    )
    {
        var normalizedAction = action?.Trim() ?? string.Empty;
        var normalizedResourceType = resourceType?.Trim() ?? string.Empty;
        var normalizedBeforeState = NormalizeOptional(beforeState);
        var normalizedAfterState = NormalizeOptional(afterState);

        if (actorUserId == Guid.Empty)
            throw new DomainException("Administrative audit actor is required.");
        if (resourceId == Guid.Empty)
            throw new DomainException("Administrative audit resource ID is required.");
        if (normalizedAction.Length is 0 or > 50)
            throw new DomainException(
                "Administrative audit action must contain between 1 and 50 characters.");
        if (normalizedResourceType.Length is 0 or > 100)
            throw new DomainException(
                "Administrative audit resource type must contain between 1 and 100 characters.");
        if (normalizedBeforeState is null && normalizedAfterState is null)
            throw new DomainException(
                "Administrative audit entry must preserve a before or after state.");

        Id = Guid.NewGuid();
        ActorUserId = actorUserId;
        Action = normalizedAction;
        ResourceType = normalizedResourceType;
        ResourceId = resourceId;
        BeforeState = normalizedBeforeState;
        AfterState = normalizedAfterState;
        OccurredAt = occurredAt;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
