using Domain.Exceptions;

namespace Domain.Entities.Supplements;

/// <summary>Prescrição direta de um suplemento a um cliente.</summary>
public class ClientSupplementAssignment
{
    public Guid Id { get; private set; }
    public Guid OwnerTrainerId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid SupplementId { get; private set; }
    public string? Dose { get; private set; }
    public string? TimingNotes { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private ClientSupplementAssignment() { }

    /// <summary>Cria uma prescrição direta de suplemento a um cliente.</summary>
    public ClientSupplementAssignment(
        Guid ownerTrainerId,
        Guid clientId,
        Guid supplementId,
        string? dose,
        string? timingNotes,
        string? notes,
        DateTime now
    )
    {
        if (ownerTrainerId == Guid.Empty || clientId == Guid.Empty || supplementId == Guid.Empty)
            throw new DomainException("Owner trainer, client and supplement are required.");

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ClientId = clientId;
        SupplementId = supplementId;
        SetInstructions(dose, timingNotes, notes);
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Atualiza apenas a prescrição específica do cliente.</summary>
    public void UpdateInstructions(
        string? dose,
        string? timingNotes,
        string? notes,
        DateTime now
    )
    {
        SetInstructions(dose, timingNotes, notes);
        UpdatedAt = now;
    }

    private void SetInstructions(string? dose, string? timingNotes, string? notes)
    {
        if (dose is { Length: > 100 } || timingNotes is { Length: > 500 })
            throw new DomainException("Supplement assignment fields exceed their maximum length.");

        Dose = NormalizeOptional(dose);
        TimingNotes = NormalizeOptional(timingNotes);
        Notes = NormalizeOptional(notes);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
