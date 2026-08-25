namespace Application.Features.Authentication.ConfirmEmail;

/// <summary>Solicita a confirmação de um email através de um token opaco.</summary>
public sealed record ConfirmEmailCommand(string Token);
