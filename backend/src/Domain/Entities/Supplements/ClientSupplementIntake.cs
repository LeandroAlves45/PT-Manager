using Domain.Exceptions;

namespace Domain.Entities.Supplements;

/// <summary>
/// Toma registada pelo cliente para uma atribuição de suplemento numa data local do
/// personal trainer. No máximo uma por atribuição e por dia; desmarcar elimina o registo.
/// </summary>
public sealed class ClientSupplementIntake
{
    public Guid Id { get; private set; }
    public Guid OwnerTrainerId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid ClientSupplementAssignmentId { get; private set; }
    public DateOnly LocalDate { get; private set; }
    public DateTime TakenAt { get; private set; }

    private ClientSupplementIntake() { }

    /// <summary>Regista a toma de hoje de uma atribuição ativa.</summary>
    public ClientSupplementIntake(
        Guid ownerTrainerId,
        Guid clientId,
        Guid clientSupplementAssignmentId,
        DateOnly localDate,
        DateTime now)
    {
        if (ownerTrainerId == Guid.Empty || clientId == Guid.Empty
            || clientSupplementAssignmentId == Guid.Empty)
            throw new DomainException(
                "Owner trainer, client and client supplement assignment are required.");

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ClientId = clientId;
        ClientSupplementAssignmentId = clientSupplementAssignmentId;
        LocalDate = localDate;
        TakenAt = now;
    }
}
