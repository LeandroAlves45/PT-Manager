namespace Application.Features.Assessments.CheckIns.Dtos;

/// <summary>
/// Próximo check-in agendado do cliente autenticado. O cliente responde sempre no próprio dia.
/// A leitura serve para avisar ao cliente com antecedência.
/// </summary>
public sealed record MyNextCheckInDto(Guid Id, DateOnly CheckInDate, bool IsToday);
