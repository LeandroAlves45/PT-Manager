using Domain.ValueObjects;

namespace Application.Features.Assessments.CheckIns.Abstractions;

/// <summary>Persiste o ciclo de vida de CheckIn com locks tenant-safe.</summary>
public interface ICheckInStore
{
    Task<CheckInStoreResult> CreateAsync(
        Guid trainerId,
        Guid clientId,
        DateOnly checkInDate,
        DateOnly? targetDate,
        DateTime now,
        CancellationToken cancellationToken
    );


    Task<CheckInStoreResult> RescheduleAsync(
        Guid trainerId,
        Guid checkInId,
        DateOnly checkInDate,
        DateOnly? targetDate,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<CheckInStoreResult> CancelAsync(
        Guid trainerId,
        Guid checkInId,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<CheckInStoreResult> SubmitResponseAsync(
        Guid trainerId,
        Guid userId,
        Guid checkInId,
        decimal weightKg,
        decimal? bodyFatPercentage,
        string? notes,
        BodyMeasurements bodyMeasurements,
        CheckInFeedback feedback,
        int? trainingAdherenceScore,
        int? nutritionAdherenceScore,
        DateTime now,
        CancellationToken cancellationToken
    );

    Task<CheckInStoreResult> CorrectAsync(
        Guid trainerId,
        Guid checkInId,
        DateOnly? targetDate,
        decimal weightKg,
        decimal? bodyFatPercentage,
        string? notes,
        BodyMeasurements bodyMeasurements,
        CheckInFeedback feedback,
        int? trainingAdherenceScore,
        int? nutritionAdherenceScore,
        DateTime now,
        CancellationToken cancellationToken
    );
}
