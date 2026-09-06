using Application.Features.Authentication.Google.Dtos;

namespace Api.Contracts.Authentication;

/// <summary>Credenciais Google Sign-In. O nonce vem do cookie HttpOnly.</summary>
public sealed record GoogleSignInRequest(string IdToken, string? InvitationToken);

/// <summary>Credenciais para associar Google a uma conta local autenticada.</summary>
public sealed record GoogleLinkRequest(string IdToken, string CurrentPassword);

/// <summary>Nonce emitido pelo challenge e espelhado no cookie HttpOnly.</summary>
public sealed record GoogleChallengeResponse(string Nonce, DateTime ExpiresAt)
{
    public static GoogleChallengeResponse From(GoogleChallengeDto dto) =>
        new(dto.Nonce, dto.ExpiresAt);
}

/// <summary>Sign-in concluído mas ainda pendente de acção do utilizador.</summary>
public sealed record GooglePendingResponse(string Status)
{
    public const string EmailConfirmationRequired = "email_confirmation_required";
}
