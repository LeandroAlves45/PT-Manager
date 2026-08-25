namespace Application.Features.Authentication.Login;

/// <summary>Credenciais apresentadas no login local.</summary>
public sealed record LoginCommand(string Email, string Password);
