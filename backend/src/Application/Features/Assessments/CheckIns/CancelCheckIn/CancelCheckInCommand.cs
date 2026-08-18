namespace Application.Features.Assessments.CheckIns.CancelCheckIn;

/// <summary>Cancela um check-in futuro sem respostas.</summary>
public sealed record CancelCheckInCommand(Guid CheckInId);
