using Application.Results;

namespace Application.Features.Authentication.Google.Abstractions;

/// <summary>Valida uma credencial externa e o nonce esperado.</summary>
public interface IExternalIdentityVerifier
{
    Task<Result<VerifiedExternalIdentity>> VerifyAsync(
        string provider,
        string idToken,
        string expectedNonce,
        CancellationToken cancellationToken);
}
