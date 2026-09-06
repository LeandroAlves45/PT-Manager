namespace Application.Features.Authentication.Google.Abstractions;

/// <summary>Challenge emitido; apenas o hash é persistido.</summary>
public sealed record IssuedExternalChallenge(string RawNonce, DateTime ExpiresAt);
