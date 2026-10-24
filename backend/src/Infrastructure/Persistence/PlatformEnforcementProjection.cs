using Application.Common;
using Domain.ValueObjects;

namespace Infrastructure.Persistence;

/// <summary>
/// Converte o estado de moderação de um item de catálogo no DTO que o personal trainer vê.
/// Chamado na projeção final das consultas: o EF materializa os três campos e a conversão
/// corre em memória.
/// </summary>
internal static class PlatformEnforcementProjection
{
    internal static PlatformEnforcementDto? From(
        PlatformEnforcementStatus status,
        PlatformEnforcementReason? reason,
        string? note) =>
        status == PlatformEnforcementStatus.Blocked && reason is not null
            ? new PlatformEnforcementDto(reason.Value, note)
            : null;
}
