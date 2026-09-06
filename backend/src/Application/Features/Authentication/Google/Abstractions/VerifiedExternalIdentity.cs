namespace Application.Features.Authentication.Google.Abstractions;

/// <summary>Identidade devolvida por um adapter depois de validar a credencial externa.</summary>
public sealed record VerifiedExternalIdentity(
    string Provider,
    string Subject,
    string Email,
    string? FullName,
    bool IsEmailAuthoritative);
