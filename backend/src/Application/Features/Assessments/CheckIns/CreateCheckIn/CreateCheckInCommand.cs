namespace Application.Features.Assessments.CheckIns.CreateCheckIn;

/// <summary>Agenda um check-in vazio para o cliente.</summary>
public sealed record CreateCheckInCommand(
    Guid ClientId,
    DateOnly CheckInDate,
    DateOnly? TargetDate
);
