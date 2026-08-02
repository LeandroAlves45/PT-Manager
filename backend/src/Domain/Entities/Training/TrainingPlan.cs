using Domain.Exceptions;
namespace Domain.Entities.Training;

/// <summary>
/// Plano de treino de um cliente. Período de datas tem que ter início mas não precisa ter fim.
/// Modalidades de treino disponíveis mas não obrigatórias.
/// </summary>
public class TrainingPlan
{
    public Guid Id { get; private set; }
    public Guid OwnerTrainerId { get; private set; }
    public Guid ClientId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public string? TrainingModality { get; private set; }
    /// <summary>
    /// Notas sobre o plano de treino, como observações do treinador
    /// </summary>
    public string? Notes { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public bool IsActive { get; private set; }
    public bool IsArchived { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private TrainingPlan() { }

    /// <summary>
    /// Cria um plano de treino ativo
    /// </summary>
    public TrainingPlan(
        Guid ownerTrainerId,
        Guid clientId,
        string name,
        string? description,
        string? trainingModality,
        string? notes,
        DateOnly startDate,
        DateOnly? endDate,
        DateTime now
    )
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        if (normalizedName.Length is 0 or > 255)
            throw new DomainException("Training plan name must be between 1 and 255 characters.");
        if (trainingModality != null && trainingModality.Length > 50)
            throw new DomainException("Training modality cannot exceed 50 characters.");
        if (endDate.HasValue && endDate.Value < startDate)
            throw new DomainException("Training plan end date cannot be before start date.");

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ClientId = clientId;
        Name = normalizedName;
        Description = NormalizeOptional(description);
        TrainingModality = NormalizeOptional(trainingModality);
        Notes = NormalizeOptional(notes);
        StartDate = startDate;
        EndDate = endDate;
        IsActive = true;
        IsArchived = false;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Arquiva um plano de treino, tornando-o inativo.</summary>
    public void Archive(DateTime now)
    {
        EnsureNotDeleted();
        IsActive = false;
        IsArchived = true;
        UpdatedAt = now;
    }

    /// <summary>Ativa um plano de treino arquivado.</summary>
    public void Activate(DateTime now)
    {
        EnsureNotDeleted();
        IsActive = true;
        IsArchived = false;
        UpdatedAt = now;
    }

    /// <summary>Soft delete de um plano de treino.</summary>
    public void Delete(DateTime now)
    {
        IsDeleted = true;
        IsActive = false;
        IsArchived = true;
        UpdatedAt = now;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("Cannot modify a deleted training plan.");
    }
}
