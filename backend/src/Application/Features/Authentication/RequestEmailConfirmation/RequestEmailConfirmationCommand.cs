namespace Application.Features.Authentication.RequestEmailConfirmation;

/// <summary>Pede um novo link de confirmação de email, sem sessão.</summary>
public sealed record RequestEmailConfirmationCommand(string Email);
