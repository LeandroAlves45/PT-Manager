using Application.Features.Authentication.Dtos;

namespace Api.Contracts.Authentication;

/// <summary>Credenciais de login.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Registo de um novo personal trainer.</summary>
public sealed record SignUpRequest(string Email, string Password, string FullName);

/// <summary>Email para o qual é pedido um novo link de confirmação.</summary>
public sealed record EmailConfirmationRequest(string Email);

/// <summary>Registo de um cliente convidado: cria a conta e abre a sessão.</summary>
public sealed record InvitedClientRegistrationRequest(
    string Token,
    string Password,
    string ConfirmPassword);

/// <summary>Token de confirmação de email.</summary>
public sealed record ConfirmEmailRequest(string Token);

/// <summary>Email para o qual é pedida a reposição de password.</summary>
public sealed record PasswordResetRequest(string Email);

/// <summary>Conclusão da reposição de password.</summary>
public sealed record PasswordResetCompletionRequest(
    string Token,
    string NewPassword,
    string ConfirmNewPassword);

/// <summary>Alteração de password de uma conta autenticada.</summary>
public sealed record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword);

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

/// <summary>
/// Resposta única do signup. Não distingue email novo de email já registado: o resultado
/// só se vê na caixa de correio.
/// </summary>
public sealed record SignUpAcceptedResponse(string Status)
{
    public const string ConfirmationEmailSent = "confirmation_email_sent";
}

/// <summary>Email, role e métodos de entrada da conta autenticada.</summary>
public sealed record AccountResponse(
    string Email,
    string Role,
    bool HasPassword,
    bool GoogleLinked)
{
    public static AccountResponse From(AccountSecurityDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new(dto.Email, dto.Role, dto.HasPassword, dto.GoogleLinked);
    }
}
