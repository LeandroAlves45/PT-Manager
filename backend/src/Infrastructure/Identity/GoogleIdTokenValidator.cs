using Google.Apis.Auth;

namespace Infrastructure.Identity;

/// <summary>Única chamada à biblioteca oficial de validação de ID tokens Google.</summary>
internal sealed class GoogleIdTokenValidator : IGoogleIdTokenValidator
{
    public Task<GoogleJsonWebSignature.Payload> ValidateAsync(
        string idToken,
        GoogleJsonWebSignature.ValidationSettings settings)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idToken);
        ArgumentNullException.ThrowIfNull(settings);

        return GoogleJsonWebSignature.ValidateAsync(idToken, settings);
    }
}
