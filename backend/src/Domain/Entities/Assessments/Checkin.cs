using Domain.Exceptions;
namespace Domain.Entities.Assessments;

/// <summary>
/// Check-in periódico de um cliente: peso e massa gorda numa data, com meta opcional
/// para a próxima avaliação.
/// </summary>
public class Checkin
{
    public Guid Id { get; private set; }
    public Guid OwnerTrainerId { get; private set; }
    public Guid ClientId { get; private set; }
    public DateOnly CheckinDate { get; private set; }
    public DateOnly? TargetDate { get; private set; }
    public decimal? WeightKg { get; private set; }
    public decimal? BodyFatPercentage { get; private set; }
    public string? Notes { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Checkin() { }

    /// <summary>Cria um check-in validando apenas os valores presentes.</summary>
    public Checkin(
        Guid ownerTrainerId,
        Guid clientId,
        DateOnly checkinDate,
        DateOnly? targetDate,
        decimal? weightKg,
        decimal? bodyFatPercentage,
        string? notes,
        DateTime now
    )
    {
        ValidateValues(targetDate, checkinDate, weightKg, bodyFatPercentage);

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ClientId = clientId;
        CheckinDate = checkinDate;
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
        ValidateValues(targetDate, CheckinDate, weightKg, bodyFatPercentage);

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
        DateOnly checkinDate,
        decimal? weightKg,
        decimal? bodyFatPercentage
    )
    {
        if (targetDate.HasValue && targetDate < checkinDate)
            throw new DomainException("Target date cannot be before check-in date");
        if (weightKg.HasValue && weightKg <= 0)
            throw new DomainException("Weight invalid");
        if (bodyFatPercentage.HasValue && (bodyFatPercentage < 0 || bodyFatPercentage > 100))
            throw new DomainException("Body fat percentage invalid");
    }
}
