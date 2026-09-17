namespace Application.Features.Assessments.CheckIns.MarkCheckInReviewed;

/// <summary>Marca como revista a resposta de um check-in.</summary>
public sealed record MarkCheckInReviewedCommand(Guid CheckInId);
