using Domain.Exceptions;
using Domain.ValueObjects;
namespace Domain.Entities.Assessments;

/// <summary>
/// Check-in periódico de um cliente: peso, massa gorda, medidas e feedback
/// qualitativo numa data, com meta opcional para a próxima avaliação.
/// </summary>
public sealed class CheckIn
{
    public Guid Id { get; private set; }
    public Guid OwnerTrainerId { get; private set; }
    public Guid ClientId { get; private set; }
    public DateOnly CheckInDate { get; private set; }
    public DateOnly? TargetDate { get; private set; }
    public decimal? WeightKg { get; private set; }
    public decimal? BodyFatPercentage { get; private set; }
    public string? Notes { get; private set; }
    public BodyMeasurements BodyMeasurements { get; private set; } = BodyMeasurements.Empty;
    public CheckInFeedback Feedback { get; private set; } = CheckInFeedback.Empty;
    public int? TrainingAdherenceScore { get; private set; }
    public int? NutritionAdherenceScore { get; private set; }
    public DateTime? RespondedAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private CheckIn() { }

    /// <summary>Agenda um CheckIn vazio para resposta futura do cliente.</summary>
    public CheckIn(
        Guid ownerTrainerId,
        Guid clientId,
        DateOnly checkInDate,
        DateOnly? targetDate,
        DateTime now
    )
    {
        if (ownerTrainerId == Guid.Empty || clientId == Guid.Empty)
            throw new DomainException("Owner trainer ID and client ID are required.");
        ValidateTargetDate(checkInDate, targetDate);

        Id = Guid.NewGuid();
        OwnerTrainerId = ownerTrainerId;
        ClientId = clientId;
        CheckInDate = checkInDate;
        TargetDate = targetDate;
        WeightKg = null;
        BodyFatPercentage = null;
        Notes = null;
        BodyMeasurements = BodyMeasurements.Empty;
        Feedback = CheckInFeedback.Empty;
        TrainingAdherenceScore = null;
        NutritionAdherenceScore = null;
        RespondedAt = null;
        CancelledAt = null;
        IsDeleted = false;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Reagenda apenas antes do dia original e antes de qualquer resposta.</summary>
    public void Reschedule(
        DateOnly newCheckInDate,
        DateOnly? newTargetDate,
        DateOnly localToday,
        DateTime now
    )
    {
        EnsureNotDeleted();
        EnsureOpen();

        if (CheckInDate <= localToday)
            throw new DomainException("A check-in can only be rescheduled before its scheduled day.");
        if (newCheckInDate <= localToday)
            throw new DomainException("The new check-in date must be in the future.");

        ValidateTargetDate(newCheckInDate, newTargetDate);

        if (CheckInDate == newCheckInDate && TargetDate == newTargetDate)
            return;

        CheckInDate = newCheckInDate;
        TargetDate = newTargetDate;
        UpdatedAt = now;
    }

    /// <summary>Cancela um CheckIn ainda futuro e sem resposta.</summary>
    public void Cancel(DateOnly localToday, DateTime now)
    {
        EnsureNotDeleted();
        if (CancelledAt.HasValue)
            return;
        if (RespondedAt.HasValue)
            throw new DomainException("An answered check-in cannot be cancelled.");
        if (CheckInDate <= localToday)
            throw new DomainException("A check-in can only be cancelled before its scheduled day.");

        CancelledAt = now;
        UpdatedAt = now;
    }

    /// <summary>Regista uma única resposta do cliente no dia agendado.</summary>
    public void SubmitResponse(
        decimal weightKg,
        decimal? bodyFatPercentage,
        string? notes,
        BodyMeasurements? bodyMeasurements,
        CheckInFeedback? feedback,
        int? trainingAdherenceScore,
        int? nutritionAdherenceScore,
        DateOnly localToday,
        DateTime now
    )
    {
        EnsureNotDeleted();
        var values = NormalizeResponse(
            weightKg, bodyFatPercentage, notes, bodyMeasurements, feedback,
            trainingAdherenceScore, nutritionAdherenceScore
        );

        // A repetição exata continua válida mesmo se a confirmação HTTP chegar mais tarde.
        if (RespondedAt.HasValue)
        {
            if (HasSameResponse(values))
                return;
            throw new DomainException("The check-in has already been answered.");
        }

        if (CancelledAt.HasValue)
            throw new DomainException("A cancelled check-in cannot be answered.");
        if (CheckInDate != localToday)
            throw new DomainException("The check-in can only be answered on its scheduled day.");

        SetResponse(values);
        RespondedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Confirma se uma repetição contém exatamente a resposta persistida.</summary>
    public bool MatchesResponse(
        decimal weightKg,
        decimal? bodyFatPercentage,
        string? notes,
        BodyMeasurements? bodyMeasurements,
        CheckInFeedback? feedback,
        int? trainingAdherenceScore,
        int? nutritionAdherenceScore
    )
    {
        var values = NormalizeResponse(
            weightKg, bodyFatPercentage, notes, bodyMeasurements, feedback,
            trainingAdherenceScore, nutritionAdherenceScore
        );
        return RespondedAt.HasValue && HasSameResponse(values);
    }

    /// <summary>Corrige uma resposta existente sem alterar autoria ou data.</summary>
    public void Correct(
        DateOnly? targetDate,
        decimal weightKg,
        decimal? bodyFatPercentage,
        string? notes,
        BodyMeasurements? bodyMeasurements,
        CheckInFeedback? feedback,
        int? trainingAdherenceScore,
        int? nutritionAdherenceScore,
        DateTime now
    )
    {
        EnsureNotDeleted();
        if (!RespondedAt.HasValue || CancelledAt.HasValue)
            throw new DomainException("Only an answered check-in can be corrected.");
        ValidateTargetDate(CheckInDate, targetDate);
        var values = NormalizeResponse(
            weightKg, bodyFatPercentage, notes, bodyMeasurements, feedback,
            trainingAdherenceScore, nutritionAdherenceScore
        );
        if (TargetDate == targetDate && HasSameResponse(values))
            return;

        TargetDate = targetDate;
        SetResponse(values);
        UpdatedAt = now;
    }

    /// <summary>Soft delete.</summary>
    public void SoftDelete(DateTime now)
    {
        if (IsDeleted)
            return;
        IsDeleted = true;
        UpdatedAt = now;
    }

    private static ResponseValues NormalizeResponse(
        decimal weightKg,
        decimal? bodyFatPercentage,
        string? notes,
        BodyMeasurements? bodyMeasurements,
        CheckInFeedback? feedback,
        int? trainingAdherenceScore,
        int? nutritionAdherenceScore
    )
    {
        if (weightKg <= 0)
            throw new DomainException("Weight must be greater than zero.");
        if (bodyFatPercentage is <= 0 or >= 100)
            throw new DomainException("Body fat percentage must be greater than zero and less than one hundred.");

        ValidateScore(trainingAdherenceScore, "Training adherence score");
        ValidateScore(nutritionAdherenceScore, "Nutrition adherence score");

        return new ResponseValues(
            weightKg,
            bodyFatPercentage,
            NormalizeOptional(notes),
            bodyMeasurements ?? BodyMeasurements.Empty,
            feedback ?? CheckInFeedback.Empty,
            trainingAdherenceScore,
            nutritionAdherenceScore
        );
    }

    private bool HasSameResponse(ResponseValues values) =>
        WeightKg == values.WeightKg &&
        BodyFatPercentage == values.BodyFatPercentage &&
        Notes == values.Notes &&
        BodyMeasurements == values.BodyMeasurements &&
        Feedback == values.Feedback &&
        TrainingAdherenceScore == values.TrainingAdherenceScore &&
        NutritionAdherenceScore == values.NutritionAdherenceScore;

    private void SetResponse(ResponseValues values)
    {
        WeightKg = values.WeightKg;
        BodyFatPercentage = values.BodyFatPercentage;
        Notes = values.Notes;
        BodyMeasurements = values.BodyMeasurements;
        Feedback = values.Feedback;
        TrainingAdherenceScore = values.TrainingAdherenceScore;
        NutritionAdherenceScore = values.NutritionAdherenceScore;
    }

    private void EnsureOpen()
    {
        if (RespondedAt.HasValue || CancelledAt.HasValue)
            throw new DomainException("Only an open check-in can be rescheduled.");
    }

    private static void ValidateTargetDate(DateOnly checkInDate, DateOnly? targetDate)
    {
        if (targetDate.HasValue && targetDate.Value < checkInDate)
            throw new DomainException("Target date cannot be before check-in date.");
    }

    private static void ValidateScore(int? score, string field)
    {
        if (score is < 0 or > 100)
            throw new DomainException($"{field} must be between 0 and 100.");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void EnsureNotDeleted()
    {
        if (IsDeleted)
            throw new DomainException("Cannot correct a deleted check-in.");
    }

    private sealed record ResponseValues(
        decimal WeightKg,
        decimal? BodyFatPercentage,
        string? Notes,
        BodyMeasurements BodyMeasurements,
        CheckInFeedback Feedback,
        int? TrainingAdherenceScore,
        int? NutritionAdherenceScore
    );
}
