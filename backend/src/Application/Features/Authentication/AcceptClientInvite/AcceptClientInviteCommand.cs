namespace Application.Features.Authentication.AcceptClientInvite;

/// <summary>Solicita a aceitação de um convite por uma conta cliente existente.</summary>
public sealed record AcceptClientInviteCommand(string Token, bool TransferApproved);
