using Domain.Exceptions;

namespace Domain.Entities.Supplements;

/// <summary>Prescrição direta de um suplemento a um cliente.</summary>
public class ClientSupplementAssignment
{
    public Guid Id { get; private set; }
    public Guid OwnerTrainerId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid SupplementId { get; private set; }
    public string ServingSize { get; private set; } = null!;
    public string Timing { get; private set; } = null!;
    public string? TrainerNotes { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private ClientSupplementAssignment() { }

    /// <summary>Cria uma prescrição direta de suplemento a um cliente.</summary>
    public ClientSupplementAssignment(
        Guid ownerTrainerId,
        Guid clientId,
        Guid supplementId,
        string servingSize,
        string timing,
        string? trainerNotes,
        DateTime now
    )
    {
        if (ownerTrainerId == Guid.Empty || clientId == Guid.Empty || supplementId == Guid.Empty)
            throw new DomainException("Owner trainer, client and supplement are required.");

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ClientId = clientId;
        SupplementId = supplementId;
        SetInstructions(servingSize, timing, trainerNotes);
        IsActive = true;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Atualiza apenas a prescrição específica do cliente.</summary>
    public void UpdateInstructions(
        string servingSize,
        string timing,
        string? trainerNotes,
        DateTime now
    )
    {
        EnsureNotDeleted();
        SetInstructions(servingSize, timing, trainerNotes);
        UpdatedAt = now;
    }

    public void Deactivate(DateTime now)
    {
        EnsureNotDeleted();
        IsActive = false;
        UpdatedAt = now;
    }

    public void Reactivate(DateTime now)
    {
        EnsureNotDeleted();
        IsActive = true;
        UpdatedAt = now;
    }

    public void SoftDelete(DateTime now)
    {
        IsDeleted = true;
        IsActive = false;
        UpdatedAt = now;
    }

    private void SetInstructions(string servingSize, string timing, string? trainerNotes)
    {
        var normalizedServingSize = servingSize?.Trim() ?? string.Empty;
        var normalizedTiming = timing?.Trim() ?? string.Empty;
        if (normalizedServingSize.Length is 0 or > 100)
            throw new DomainException("Serving size must contain between 1 and 100 characters.");
        if (normalizedTiming.Length is 0 or > 255)
            throw new DomainException("Timing must contain between 1 and 255 characters.");

        ServingSize = normalizedServingSize;
        Timing = normalizedTiming;
        TrainerNotes = NormalizeOptional(trainerNotes);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("Cannot modify a deleted supplement assignment.");
    }
}
