using Application.Features.Authentication.Dtos;

namespace Api.Contracts.Authentication;

/// <summary>Credenciais de login.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Registo de um novo personal trainer.</summary>
public sealed record SignUpRequest(string Email, string Password, string FullName);

/// <summary>Token de confirmação de email.</summary>
public sealed record ConfirmEmailRequest(string Token);

/// <summary>Email para o qual é pedida a reposição de password.</summary>
public sealed record PasswordResetRequest(string Email);

/// <summary>Conclusão da reposição de password.</summary>
public sealed record PasswordResetCompletionRequest(
    string Token,
    string NewPassword,
    string ConfirmPassword);

/// <summary>Aceitação de um convite de cliente.</summary>
public sealed record AcceptInvitationRequest(string Token, bool TransferApproved);

/// <summary>Emissão de um convite para um cliente do personal trainer autenticado.</summary>
public sealed record InviteClientRequest(Guid ClientId, string Email);

/// <summary>Sessão devolvida ao cliente. O refresh token nunca aparece aqui.</summary>
public sealed record SessionResponse(
    Guid UserId,
    Guid? TrainerId,
    string Role,
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string CsrfToken)
{
    /// <summary>Projeta a sessão da Application omitindo o refresh token.</summary>
    public static SessionResponse From(AuthenticationSessionDto session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new(
            session.UserId,
            session.TrainerId,
            session.Role,
            session.AccessToken,
            session.AccessTokenExpiresAt,
            session.RawCsrfToken
        );
    }
}

/// <summary>Segredo anti-CSRF devolvido pelo bootstrap.</summary>
public sealed record CsrfResponse(string CsrfToken)
{
    public static CsrfResponse From(CsrfTokenDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new(dto.RawCsrfToken);
    }
}

/// <summary>Identificador do personal trainer registado.</summary>
public sealed record SignUpResponse(
    Guid UserId,
    Guid TrainerId,
    string Email,
    DateTime TrialEndsAt
)
{
    public static SignUpResponse From(RegisteredTrainerDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new(
            dto.UserId,
            dto.TrainerId,
            dto.Email,
            dto.TrialEndsAt
        );
    }
}
