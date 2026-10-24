using Application.Common;

namespace Api.Contracts.Common;

/// <summary>
/// Bloqueio pela moderação de um item usado num plano: o motivo (um dos quatro códigos) e a
/// nota do superuser. Só o personal trainer dono o recebe; null quando o item não está bloqueado.
/// </summary>
public sealed record PlatformEnforcementResponse(string Reason, string? Note)
{
    /// <summary>Projeta o DTO da Application; null continua null.</summary>
    public static PlatformEnforcementResponse? From(PlatformEnforcementDto? dto) =>
        dto is null ? null : new PlatformEnforcementResponse(dto.Reason, dto.Note);
}
