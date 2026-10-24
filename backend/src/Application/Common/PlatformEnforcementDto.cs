namespace Application.Common;

/// <summary>
/// Bloqueio pela moderação de um item de catálogo usado num plano, visto pelo personal trainer
/// dono: o motivo e a nota do superuser. Null quando o item não está bloqueado. O cliente nunca
/// recebe isto: no portal o item bloqueado simplesmente não aparece.
/// </summary>
public sealed record PlatformEnforcementDto(string Reason, string? Note);
