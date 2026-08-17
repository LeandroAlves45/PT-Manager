namespace Application.Features.Sessions.Dtos;

/// <summary>Representa uma sessão visível na fronteira de Application.</summary>
public sealed record SessionDto(
    Guid Id,
    Guid ClientId,
    Guid? ClientSessionPackId,
    DateTimeOffset StartsAt,
    int DurationMinutes,
    string? Location,
    string? SessionType,
    string? Notes,
    string Status,
    DateTime StatusChangedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
