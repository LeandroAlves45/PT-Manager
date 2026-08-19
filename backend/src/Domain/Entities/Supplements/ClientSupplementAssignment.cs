using Domain.Exceptions;
namespace Domain.Entities.Supplements;

/// <summary>Prescrição direta de um suplemento a um cliente.</summary>
public sealed class ClientSupplementAssignment
{
    public Guid Id { get; private set; }
    public Guid OwnerTrainerId { get; private set; }
    public Guid ClientId { get; private set; }
    public Guid SupplementId { get; private set; }
    public string ServingSize { get; private set; } = null!;
    public string Timing { get; private set; } = null!;
    public string? TrainerNotes { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private ClientSupplementAssignment() { }

    /// <summary>Cria uma atribuição ativa com instruções independentes do catálogo.</summary>
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
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Altera apenas as instruções personalizadas da atribuição.</summary>
    public void UpdateInstructions(
        string servingSize,
        string timing,
        string? trainerNotes,
        DateTime now
    )
    {
        SetInstructions(servingSize, timing, trainerNotes);
        UpdatedAt = now;
    }

    /// <summary>Interrompe a prescrição sem eliminar o histórico.</summary>
    public void Deactivate(DateTime now)
    {
        IsActive = false;
        UpdatedAt = now;
    }

    /// <summary>Retoma uma prescrição existente.</summary>
    public void Reactivate(DateTime now)
    {
        IsActive = true;
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
}
