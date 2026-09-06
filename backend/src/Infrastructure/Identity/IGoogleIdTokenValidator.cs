using Google.Apis.Auth;

namespace Infrastructure.Identity;

/// <summary>Seam interno em torno da validação estática do Google.Apis.Auth.</summary>
internal interface IGoogleIdTokenValidator
{
    Task<GoogleJsonWebSignature.Payload> ValidateAsync(
        string idToken,
        GoogleJsonWebSignature.ValidationSettings settings);
}
