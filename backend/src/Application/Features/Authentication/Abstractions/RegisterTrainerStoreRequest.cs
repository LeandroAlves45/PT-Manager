namespace Application.Features.Authentication.Abstractions;

/// <summary>Dados necessários para o registo de um personal trainer.</summary>
public sealed record RegisterTrainerStoreRequest(
    string Email,
    string Password,
    string FullName,
    DateTime TrialEndsAt,
    DateTime EmailConfirmationExpiresAt
);
