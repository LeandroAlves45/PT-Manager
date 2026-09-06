namespace Application.Features.Authentication.Google.Link;

/// <summary>Credencial Google, nonce e password actual para linking explícito.</summary>
public sealed record GoogleLinkCommand(
    string IdToken,
    string RawNonce,
    string CurrentPassword);
