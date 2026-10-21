namespace Application.Features.Authentication.InviteClient;

/// <summary>
/// Convite emitido: o link a copiar, até quando é válido (UTC) e se o email saiu. Com
/// <c>EmailSent</c> falso o convite existe e serve na mesma; o personal trainer envia o link
/// por outro canal.
/// </summary>
public sealed record InviteClientResultDto(string InviteUrl, DateTime ExpiresAt, bool EmailSent);
