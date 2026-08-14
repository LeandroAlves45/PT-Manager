using Domain.Exceptions;
namespace Domain.Entities.Training;

/// <summary>
/// Plano de treino de um cliente. Período de datas tem que ter início mas não precisa ter fim.
/// Modalidades de treino disponíveis mas não obrigatórias.
/// </summary>
public sealed class TrainingPlan
{
    private readonly List<TrainingPlanDay> _days = [];

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
    public IReadOnlyCollection<TrainingPlanDay> Days => _days;

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
        ValidateIdentifiers(ownerTrainerId, clientId);
        ValidateMetadata(name, trainingModality, startDate, endDate);

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ClientId = clientId;
        ApplyMetadata(name, description, trainingModality, notes, startDate, endDate);
        IsActive = true;
        IsArchived = false;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Substitui metadados sem alterar identidade ou estrutura.</summary>
    public void UpdateMetadata(
        string name,
        string? description,
        string? trainingModality,
        string? notes,
        DateOnly startDate,
        DateOnly? endDate,
        DateTime now
    )
    {
        EnsureModifiable();
        ValidateMetadata(name, trainingModality, startDate, endDate);
        ApplyMetadata(name, description, trainingModality, notes, startDate, endDate);
        UpdatedAt = now;
    }

    /// <summary>Adiciona um dia com par semana/dia único ao plano.</summary>
    public TrainingPlanDay AddDay(
        int dayOfWeek,
        int weekNumber,
        string? notes,
        DateTime now
    )
    {
        EnsureModifiable();
        if (_days.Any(day => day.DayOfWeek == dayOfWeek && day.WeekNumber == weekNumber))
            throw new DomainException("A training day already exists for this week and weekday.");

        var day = new TrainingPlanDay(Id, dayOfWeek, weekNumber, notes, now);
        _days.Add(day);
        UpdatedAt = now;
        return day;
    }

    /// <summary>Obtém um dia pertencente ao agregado.</summary>
    public TrainingPlanDay GetDay(Guid dayId) =>
        _days.SingleOrDefault(day => day.Id == dayId)
        ?? throw new DomainException("Training day does not belong to this plan.");

    /// <summary>Atualiza semana, dia e notas depois da autorização da Application.</summary>
    public void UpdateDay(
        Guid dayId,
        int dayOfWeek,
        int weekNumber,
        string? notes,
        DateTime now
    )
    {
        EnsureModifiable();
        var day = GetDay(dayId);
        if (_days.Any(candidate => candidate.Id != dayId &&
            candidate.DayOfWeek == dayOfWeek && candidate.WeekNumber == weekNumber))
            throw new DomainException("A training day already exists for this week and weekday.");

        day.Update(dayOfWeek, weekNumber, notes, now);
        UpdatedAt = now;
    }

    /// <summary>Remove um dia autorizado como não histórico.</summary>
    public void RemoveDay(Guid dayId, DateTime now)
    {
        EnsureModifiable();
        var day = GetDay(dayId);
        _days.Remove(day);
        UpdatedAt = now;
    }

    /// <summary>Arquiva um plano de treino, tornando-o inativo.</summary>
    public void Archive(DateTime now)
    {
        EnsureNotDeleted();
        if (IsArchived)
            return;
        IsActive = false;
        IsArchived = true;
        UpdatedAt = now;
    }

    /// <summary>Soft delete de um plano de treino.</summary>
    public void SoftDelete(DateTime now)
    {
        if (IsDeleted)
            return;
        IsDeleted = true;
        IsActive = false;
        IsArchived = true;
        UpdatedAt = now;
    }

    private void ApplyMetadata(
        string name,
        string? description,
        string? trainingModality,
        string? notes,
        DateOnly startDate,
        DateOnly? endDate
    )
    {
        Name = name.Trim();
        Description = NormalizeOptional(description);
        TrainingModality = NormalizeOptional(trainingModality);
        Notes = NormalizeOptional(notes);
        StartDate = startDate;
        EndDate = endDate;
    }

    private static void ValidateIdentifiers(Guid ownerTrainerId, Guid clientId)
    {
        if (ownerTrainerId == Guid.Empty)
            throw new DomainException("Owner trainer ID is required.");
        if (clientId == Guid.Empty)
            throw new DomainException("Client ID is required.");
    }

    private static void ValidateMetadata(
        string name,
        string? trainingModality,
        DateOnly startDate,
        DateOnly? endDate
    )
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        if (normalizedName.Length is 0 or > 255)
            throw new DomainException("Training plan name must be between 1 and 255 characters.");
        if (trainingModality is not null && trainingModality.Trim().Length > 50)
            throw new DomainException("Training modality cannot exceed 50 characters.");
        if (endDate.HasValue && endDate.Value < startDate)
            throw new DomainException("Training plan end date cannot be before start date.");
    }

    /// <summary>Ativa um plano de treino arquivado.</summary>
    public void Activate(DateTime now)
    {
        EnsureNotDeleted();
        IsActive = true;
        IsArchived = false;
        UpdatedAt = now;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void EnsureModifiable()
    {
        EnsureNotDeleted();
        if (IsArchived)
            throw new DomainException("Cannot modify an archived training plan.");
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("Cannot modify a deleted training plan.");
    }
}
