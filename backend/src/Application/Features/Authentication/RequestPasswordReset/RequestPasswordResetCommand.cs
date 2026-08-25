namespace Application.Features.Authentication.RequestPasswordReset;

/// <summary>Solicita um link de reset para um email.</summary>
public sealed record RequestPasswordResetCommand(string Email);
