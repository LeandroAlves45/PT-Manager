namespace Application.Features.Authentication.RegisterInvitedClient;

/// <summary>Cria a conta de um cliente convidado a partir do token de convite.</summary>
public sealed record RegisterInvitedClientCommand(
    string Token,
    string Password,
    string ConfirmPassword);
