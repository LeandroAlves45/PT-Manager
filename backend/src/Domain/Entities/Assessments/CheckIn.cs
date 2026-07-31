using Domain.Exceptions;
namespace Domain.Entities.Assessments;

/// <summary>
/// Check-in periódico de um cliente: peso e massa gorda numa data, com meta opcional
/// para a próxima avaliação.
/// </summary>
public class CheckIn
{
    public Guid Id { get; private set; }
    public Guid OwnerTrainerId { get; private set; }
    public Guid ClientId { get; private set; }
    public DateOnly CheckInDate { get; private set; }
    public DateOnly? TargetDate { get; private set; }
    public decimal? WeightKg { get; private set; }
    public decimal? BodyFatPercentage { get; private set; }
    public string? Notes { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private CheckIn() { }

    /// <summary>Cria um check-in validando apenas os valores presentes.</summary>
    public CheckIn(
        Guid ownerTrainerId,
        Guid clientId,
        DateOnly checkInDate,
        DateOnly? targetDate,
        decimal? weightKg,
        decimal? bodyFatPercentage,
        string? notes,
        DateTime now
    )
    {
        ValidateValues(targetDate, checkInDate, weightKg, bodyFatPercentage);

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ClientId = clientId;
        CheckInDate = checkInDate;
        TargetDate = targetDate;
        WeightKg = weightKg;
        BodyFatPercentage = bodyFatPercentage;
        Notes = notes;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Corrige medidas de um check-in.</summary>
    public void Correct(
        DateOnly? targetDate,
        decimal? weightKg,
        decimal? bodyFatPercentage,
        string? notes,
        DateTime now
    )
    {
        EnsureNotDeleted();
        ValidateValues(targetDate, CheckInDate, weightKg, bodyFatPercentage);

        TargetDate = targetDate;
        WeightKg = weightKg;
        BodyFatPercentage = bodyFatPercentage;
        Notes = notes;
        UpdatedAt = now;
    }

    /// <summary>Soft delete.</summary>
    public void SoftDelete(DateTime now)
    {
        IsDeleted = true;
        UpdatedAt = now;
    }

    /// <summary>Valida os valores do check-in.</summary>
    private void ValidateValues(
        DateOnly? targetDate,
        DateOnly checkInDate,
        decimal? weightKg,
        decimal? bodyFatPercentage
    )
    {
        if (targetDate.HasValue && targetDate < checkInDate)
            throw new DomainException("Target date cannot be before check-in date");
        if (weightKg.HasValue && weightKg <= 0)
            throw new DomainException("Weight invalid");
        if (bodyFatPercentage.HasValue && (bodyFatPercentage < 0 || bodyFatPercentage > 100))
            throw new DomainException("Body fat percentage invalid");
    }

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("Cannot correct a deleted check-in.");
    }
}
