namespace Application.Features.Clients.Dtos;

/// <summary>
/// Leitura crua do convite: a conta associada à ficha e o convite por usar mais recente.
/// O estado (pendente ou expirado) decide-se no handler, com o relógio.
/// </summary>
public sealed record ClientInvitationSnapshot(
    bool HasAccount,
    string? PendingEmail,
    DateTime? PendingExpiresAt);

/// <summary>Estados do convite mostrados na ficha.</summary>
public static class ClientInvitationStates
{
    /// <summary>Nunca foi enviado convite, ou não há nenhum por usar.</summary>
    public const string None = "none";

    /// <summary>Enviado e ainda dentro da validade.</summary>
    public const string Pending = "pending";

    /// <summary>Enviado e passou a validade sem ser aceite.</summary>
    public const string Expired = "expired";

    /// <summary>O cliente já tem conta: a ficha deixa de mostrar o convite.</summary>
    public const string Accepted = "accepted";
}

/// <summary>
/// Estado do convite do portal. <c>Email</c> é o do convite, que pode diferir do email atual da
/// ficha se este mudou depois do envio; <c>ExpiresAt</c> em UTC. Ambos nulos em none/accepted.
/// </summary>
public sealed record ClientInvitationDto(string State, string? Email, DateTime? ExpiresAt);
