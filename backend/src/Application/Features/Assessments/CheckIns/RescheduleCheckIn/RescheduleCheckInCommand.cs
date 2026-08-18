namespace Application.Features.Assessments.CheckIns.RescheduleCheckIn;

/// <summary>Altera a data de um check-in ainda futuro.</summary>
public sealed record RescheduleCheckInCommand(
    Guid CheckInId,
    DateOnly CheckInDate,
    DateOnly? TargetDate
);
