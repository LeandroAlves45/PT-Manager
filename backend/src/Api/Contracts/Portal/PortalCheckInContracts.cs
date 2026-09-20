using Application.Features.Assessments.CheckIns.Dtos;

namespace Api.Contracts.Portal;

/// <summary>Próximo check-in agendado do cliente.</summary>
public sealed record MyNextCheckInResponse(Guid Id, DateOnly CheckInDate, bool IsToday)
{
    public static MyNextCheckInResponse From(MyNextCheckInDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new MyNextCheckInResponse(
            dto.Id, dto.CheckInDate, dto.IsToday);
    }
}
